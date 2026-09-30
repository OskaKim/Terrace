using System.Diagnostics;
using Terrace.MasterData.Builder.Build;
using Terrace.MasterData.Builder.Diagnostics;

namespace Terrace.MasterData.Builder.Cli;

/// <summary>CLI 本体。テストから呼べるよう Console に直接依存せず TextWriter へ書く。</summary>
public static class BuildCommand
{
    public const int ExitSuccess = 0;
    public const int ExitFailure = 1;
    public const int ExitUsage = 2;

    public static int Run(string[] args, TextWriter output)
    {
        var options = CommandLineOptions.Parse(args);
        if (options.Help)
        {
            output.WriteLine(CommandLineOptions.Usage);
            return ExitSuccess;
        }
        if (options.Error is not null)
        {
            output.WriteLine($"引数エラー: {options.Error}");
            output.WriteLine();
            output.WriteLine(CommandLineOptions.Usage);
            return ExitUsage;
        }

        var input = Path.GetFullPath(options.Input!);
        var outputDirectory = Path.GetFullPath(options.Output!);
        var stopwatch = Stopwatch.StartNew();

        output.WriteLine($"masterdata-build: 入力 {input}");
        output.WriteLine($"masterdata-build: 出力 {outputDirectory}");

        BuildResult result;
        try
        {
            result = BuildPipeline.Run(input);
        }
        catch (Exception ex)
        {
            output.WriteLine($"内部エラー: {ex.GetType().Name}: {ex.Message}");
            if (options.Verbose) output.WriteLine(ex.ToString());
            return ExitFailure;
        }

        if (options.Verbose)
        {
            output.WriteLine($"テーブル定義 ({result.Tables.Count}):");
            foreach (var table in result.Tables)
            {
                var source = table.CsvPath is null ? "(CSV なし)" : $"{Path.GetFileName(table.CsvPath)}: {table.RowCount} 行";
                output.WriteLine($"  {table.Schema.TableName,-16} {table.Schema.Type.FullName}  {source}");
            }
        }

        PrintDiagnostics(output, "警告", result.Diagnostics.Warnings.ToList());
        PrintDiagnostics(output, "エラー", result.Diagnostics.Errors.ToList());

        if (options.Verbose && result.RawValidationOutput is not null)
        {
            output.WriteLine("--- MasterMemory の生の検証結果 ---");
            output.Write(result.RawValidationOutput);
            output.WriteLine("-----------------------------------");
        }

        if (!result.Success)
        {
            var note = result.ValidationSkipped
                ? " ※ CSV の読み込み・型変換でエラーがあるため、MasterMemory の検証(主キー重複・参照など)は実行していません"
                : "";
            output.WriteLine($"失敗: エラー {result.Diagnostics.ErrorCount} 件、警告 {result.Diagnostics.WarningCount} 件 ({stopwatch.ElapsedMilliseconds} ms){note}");
            return ExitFailure;
        }

        var (binaryPath, manifestPath) = BuildPipeline.WriteOutputs(result, outputDirectory);
        output.WriteLine($"{Path.GetFileName(binaryPath)}: {result.Binary!.Length} bytes, sha256 = {result.Manifest!.Sha256}");
        output.WriteLine($"{Path.GetFileName(manifestPath)}: 書き出しました");
        output.WriteLine($"成功: {result.Tables.Count} テーブル、{result.TotalRowCount} 行、警告 {result.Diagnostics.WarningCount} 件 ({stopwatch.ElapsedMilliseconds} ms)");
        return ExitSuccess;
    }

    private static void PrintDiagnostics(TextWriter output, string label, IReadOnlyList<Diagnostic> items)
    {
        if (items.Count == 0) return;
        output.WriteLine($"=== {label} ({items.Count} 件) ===");
        foreach (var item in items)
        {
            output.WriteLine(item.Format());
        }
    }
}
