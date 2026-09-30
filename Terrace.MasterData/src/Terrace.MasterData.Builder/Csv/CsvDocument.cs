namespace Terrace.MasterData.Builder.Csv;

/// <summary>CSV の 1 データ行。Line は物理行番号(1 始まり)。</summary>
public sealed record CsvRow(int Line, string[] Cells);

/// <summary>解析済みの CSV 1 ファイル。</summary>
public sealed class CsvDocument
{
    public CsvDocument(string filePath, int headerLine, string[] header, IReadOnlyList<CsvRow> rows)
    {
        FilePath = filePath;
        HeaderLine = headerLine;
        Header = header;
        Rows = rows;
    }

    public string FilePath { get; }
    public string FileName => Path.GetFileName(FilePath);

    /// <summary>ヘッダ行の物理行番号。ヘッダが無い場合は 0。</summary>
    public int HeaderLine { get; }
    public string[] Header { get; }
    public IReadOnlyList<CsvRow> Rows { get; }
}
