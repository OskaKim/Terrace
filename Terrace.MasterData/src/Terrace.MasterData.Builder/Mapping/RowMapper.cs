using System.Reflection;
using Terrace.MasterData.Builder.Csv;
using Terrace.MasterData.Builder.Diagnostics;
using Terrace.MasterData.Builder.Discovery;
using Terrace.MasterData.Builder.Tracing;

namespace Terrace.MasterData.Builder.Mapping;

/// <summary>
/// CSV の行をリフレクションでオブジェクトへ写す。
/// - ヘッダ名 == プロパティ名(大文字小文字は区別しない)
/// - 型定義にあるのに CSV に列がない → エラー(そのファイルは処理しない)
/// - CSV にあるのに型定義にない列 → 警告(無視して続行)
/// - セルの型変換に失敗した行は結果に含めず、エラーとして記録する
/// 生成したオブジェクトは <see cref="RowTraceRegistry"/> に由来(ファイル・行番号)を登録する。
/// </summary>
public static class RowMapper
{
    public static IReadOnlyList<object> Map(TableSchema schema, CsvDocument document, RowTraceRegistry registry, DiagnosticBag diagnostics)
    {
        var fileName = document.FileName;
        var header = document.Header;

        var columns = new List<(PropertyInfo Property, int Index)>();
        var matched = new HashSet<int>();
        var headerOk = true;

        foreach (var property in schema.Properties)
        {
            var index = Array.FindIndex(header, h => string.Equals(h, property.Name, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                diagnostics.Error(fileName, document.HeaderLine == 0 ? null : document.HeaderLine, property.Name,
                    $"CSV に列がありません ({schema.Type.Name}.{property.Name} に対応する列が必要です)");
                headerOk = false;
                continue;
            }
            columns.Add((property, index));
            matched.Add(index);
        }

        for (var i = 0; i < header.Length; i++)
        {
            if (!matched.Contains(i) && header[i].Length > 0)
            {
                diagnostics.Warning(fileName, document.HeaderLine, header[i],
                    $"{schema.Type.Name} に対応するプロパティがないため無視します");
            }
        }

        if (!headerOk)
        {
            return Array.Empty<object>();
        }

        var result = new List<object>(document.Rows.Count);
        foreach (var row in document.Rows)
        {
            object instance;
            try
            {
                instance = Activator.CreateInstance(schema.Type)
                    ?? throw new InvalidOperationException($"{schema.Type.FullName} のインスタンスを生成できません");
            }
            catch (Exception ex) when (ex is MissingMethodException or MemberAccessException)
            {
                diagnostics.Error(fileName, null, null,
                    $"{schema.Type.Name} に引数なしのコンストラクタが必要です");
                return Array.Empty<object>();
            }

            var rowOk = true;
            foreach (var (property, index) in columns)
            {
                var cell = row.Cells[index];
                if (!ValueConverter.TryConvert(cell, property.PropertyType, out var value, out var error))
                {
                    diagnostics.Error(fileName, row.Line, property.Name, error!);
                    rowOk = false;
                    continue;
                }
                property.SetValue(instance, value);
            }

            if (!rowOk) continue;

            registry.Register(schema, instance, new SourceLocation(document.FilePath, row.Line));
            result.Add(instance);
        }

        return result;
    }
}
