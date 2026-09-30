namespace Terrace.MasterData.Builder.Diagnostics;

/// <summary>
/// 警告・エラーの収集器。fail-fast にせず全件をためてからまとめて出力するために使う。
/// 同一内容の重複は 1 件にまとめる。
/// </summary>
public sealed class DiagnosticBag
{
    private readonly List<Diagnostic> _items = new();
    private readonly HashSet<string> _seen = new(StringComparer.Ordinal);

    public IReadOnlyList<Diagnostic> All => _items;
    public IEnumerable<Diagnostic> Errors => _items.Where(x => x.Severity == DiagnosticSeverity.Error);
    public IEnumerable<Diagnostic> Warnings => _items.Where(x => x.Severity == DiagnosticSeverity.Warning);
    public int ErrorCount => Errors.Count();
    public int WarningCount => Warnings.Count();
    public bool HasErrors => _items.Any(x => x.Severity == DiagnosticSeverity.Error);

    public void Add(Diagnostic diagnostic)
    {
        if (_seen.Add($"{diagnostic.Severity}|{diagnostic.Format()}"))
        {
            _items.Add(diagnostic);
        }
    }

    public void Error(string? fileName, int? line, string? column, string message)
        => Add(new Diagnostic(DiagnosticSeverity.Error, fileName, line, column, message));

    public void Warning(string? fileName, int? line, string? column, string message)
        => Add(new Diagnostic(DiagnosticSeverity.Warning, fileName, line, column, message));
}
