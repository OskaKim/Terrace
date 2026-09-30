using Terrace.MasterData.Builder.Diagnostics;
using Terrace.MasterData.Builder.Discovery;
using Terrace.MasterData.Builder.Output;

namespace Terrace.MasterData.Builder.Build;

/// <summary>1 テーブル分の処理結果。</summary>
public sealed record TableBuildInfo(TableSchema Schema, string? CsvPath, int RowCount);

public sealed class BuildResult
{
    public BuildResult(DiagnosticBag diagnostics, IReadOnlyList<TableBuildInfo> tables, byte[]? binary, Manifest? manifest, bool validationSkipped)
    {
        Diagnostics = diagnostics;
        Tables = tables;
        Binary = binary;
        Manifest = manifest;
        ValidationSkipped = validationSkipped;
    }

    public DiagnosticBag Diagnostics { get; }
    public IReadOnlyList<TableBuildInfo> Tables { get; }

    /// <summary>成功時のみ非 null。</summary>
    public byte[]? Binary { get; }

    /// <summary>成功時のみ非 null。</summary>
    public Manifest? Manifest { get; }

    /// <summary>CSV の読み込み・型変換でエラーがあり、MasterMemory の検証を実行しなかった場合 true。</summary>
    public bool ValidationSkipped { get; }

    /// <summary>MasterMemory の FormatFailedResults() そのまま(--verbose 用)。</summary>
    public string? RawValidationOutput { get; init; }

    public bool Success => !Diagnostics.HasErrors && Binary is not null;

    public int TotalRowCount => Tables.Sum(t => t.RowCount);
}
