using System.Reflection;
using System.Text;
using Terrace.MasterData.Builder.Csv;
using Terrace.MasterData.Builder.Diagnostics;
using Terrace.MasterData.Builder.Discovery;
using Terrace.MasterData.Builder.Mapping;
using Terrace.MasterData.Builder.Output;
using Terrace.MasterData.Builder.Tracing;
using Terrace.MasterData.Builder.Validation;

namespace Terrace.MasterData.Builder.Build;

/// <summary>
/// CSV フォルダ → master.bytes + manifest.json の変換パイプライン。
///
/// 1. [MemoryTable] 型をリフレクションで収集
/// 2. テーブル名と同名の CSV を読み込み、リフレクションでオブジェクトへ写す(由来を台帳に登録)
/// 3. 生成された DatabaseBuilder に Append してバイナリ化
/// 4. バイナリを MemoryDatabase として読み戻し、MasterMemory の Validate を実行
/// 5. 検証失敗をファイル名・行番号へ逆引きして報告
/// エラーは途中で止めず全件収集する。
/// </summary>
public static class BuildPipeline
{
    public const string BinaryFileName = "master.bytes";
    public const string ManifestFileName = "manifest.json";

    public static BuildResult Run(string inputDirectory, IReadOnlyList<TableSchema>? schemas = null)
    {
        schemas ??= TableDiscovery.DiscoverDefault();
        var diagnostics = new DiagnosticBag();
        var registry = new RowTraceRegistry();
        var tables = new List<TableBuildInfo>();
        var rowsByTable = new Dictionary<TableSchema, IReadOnlyList<object>>();

        if (!Directory.Exists(inputDirectory))
        {
            diagnostics.Error(null, null, null, $"入力フォルダが見つかりません: {inputDirectory}");
            return new BuildResult(diagnostics, tables, null, null, validationSkipped: true);
        }

        var csvFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in Directory.EnumerateFiles(inputDirectory, "*.csv", SearchOption.TopDirectoryOnly).OrderBy(p => p, StringComparer.Ordinal))
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (!csvFiles.TryAdd(name, path))
            {
                diagnostics.Error(Path.GetFileName(path), null, null, "大文字小文字だけが異なる同名の CSV が複数あります");
            }
        }

        var consumed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var schema in schemas)
        {
            if (!csvFiles.TryGetValue(schema.TableName, out var csvPath))
            {
                diagnostics.Error(schema.CsvFileName, null, null, $"CSV ファイルが見つかりません (テーブル {schema.Type.Name} に必要です)");
                tables.Add(new TableBuildInfo(schema, null, 0));
                continue;
            }

            consumed.Add(schema.TableName);
            var document = CsvParser.ParseFile(csvPath, diagnostics);
            var rows = RowMapper.Map(schema, document, registry, diagnostics);
            rowsByTable[schema] = rows;
            tables.Add(new TableBuildInfo(schema, csvPath, rows.Count));
        }

        foreach (var (name, path) in csvFiles)
        {
            if (!consumed.Contains(name))
            {
                diagnostics.Warning(Path.GetFileName(path), null, null, "対応するテーブル定義 ([MemoryTable]) がないため無視します");
            }
        }

        if (diagnostics.HasErrors)
        {
            return new BuildResult(diagnostics, tables, null, null, validationSkipped: true);
        }

        var builder = new DatabaseBuilder();
        foreach (var schema in schemas)
        {
            AppendTable(builder, schema, rowsByTable[schema]);
        }
        var binary = builder.Build();

        var database = new MemoryDatabase(binary);
        var validation = database.Validate();
        var raw = validation.IsValidationFailed ? validation.FormatFailedResults() : null;
        var translator = new ValidationFailureTranslator(registry, schemas);
        foreach (var failure in validation.FailedResults)
        {
            translator.Translate(failure, diagnostics);
        }

        if (diagnostics.HasErrors)
        {
            return new BuildResult(diagnostics, tables, null, null, validationSkipped: false) { RawValidationOutput = raw };
        }

        var manifest = Manifest.Create(binary, tables);
        return new BuildResult(diagnostics, tables, binary, manifest, validationSkipped: false);
    }

    public static (string BinaryPath, string ManifestPath) WriteOutputs(BuildResult result, string outputDirectory)
    {
        if (!result.Success || result.Binary is null || result.Manifest is null)
        {
            throw new InvalidOperationException("ビルドが成功していないため出力できません");
        }

        Directory.CreateDirectory(outputDirectory);
        var binaryPath = Path.Combine(outputDirectory, BinaryFileName);
        var manifestPath = Path.Combine(outputDirectory, ManifestFileName);
        File.WriteAllBytes(binaryPath, result.Binary);
        File.WriteAllText(manifestPath, result.Manifest.ToJson(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return (binaryPath, manifestPath);
    }

    /// <summary>生成された DatabaseBuilder.Append(IEnumerable&lt;T&gt;) をリフレクションで呼ぶ。</summary>
    private static void AppendTable(DatabaseBuilder builder, TableSchema schema, IReadOnlyList<object> rows)
    {
        var array = Array.CreateInstance(schema.Type, rows.Count);
        for (var i = 0; i < rows.Count; i++)
        {
            array.SetValue(rows[i], i);
        }

        var parameterType = typeof(IEnumerable<>).MakeGenericType(schema.Type);
        var append = typeof(DatabaseBuilder)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "Append" && m.GetParameters() is [var p] && p.ParameterType == parameterType)
            ?? throw new InvalidOperationException(
                $"DatabaseBuilder に {schema.Type.Name} 用の Append がありません。MasterMemory の Source Generator が走っているか確認してください。");

        append.Invoke(builder, new object[] { array });
    }
}
