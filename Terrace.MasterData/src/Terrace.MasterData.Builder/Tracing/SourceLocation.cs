namespace Terrace.MasterData.Builder.Tracing;

/// <summary>CSV 上の由来(ファイルパスと物理行番号。1 始まり、コメント行や空行も数える)。</summary>
public readonly record struct SourceLocation(string FilePath, int Line)
{
    public string FileName => Path.GetFileName(FilePath);

    public override string ToString() => $"{FileName}:{Line}";
}
