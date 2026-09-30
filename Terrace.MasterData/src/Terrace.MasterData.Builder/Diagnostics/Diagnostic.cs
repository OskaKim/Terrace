namespace Terrace.MasterData.Builder.Diagnostics;

public enum DiagnosticSeverity
{
    Warning,
    Error,
}

/// <summary>
/// 1 件の警告またはエラー。どのファイルの何行目・どの列に由来するかを保持する。
/// 出力書式は <c>item.csv:14  [ItemId] メッセージ</c>。
/// </summary>
public sealed record Diagnostic(
    DiagnosticSeverity Severity,
    string? FileName,
    int? Line,
    string? Column,
    string Message)
{
    /// <summary>"item.csv:14" / "item.csv" / ""(位置不明)</summary>
    public string Location => FileName is null ? "" : Line is null ? FileName : $"{FileName}:{Line}";

    public string Format()
    {
        var column = Column is null ? "" : $"[{Column}] ";
        return Location.Length == 0 ? column + Message : $"{Location}  {column}{Message}";
    }

    public override string ToString() => Format();
}
