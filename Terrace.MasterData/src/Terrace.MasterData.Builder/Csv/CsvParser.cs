using System.Text;
using Terrace.MasterData.Builder.Diagnostics;

namespace Terrace.MasterData.Builder.Csv;

/// <summary>
/// 行番号を保持する CSV パーサ。
/// - 1 行目(コメント・空行を除く最初の行)がヘッダ
/// - `#` で始まる行はコメントとして読み飛ばす(行番号には数える)
/// - 空行は読み飛ばす
/// - ダブルクォートで囲んだセルはカンマを含められる。"" は " のエスケープ。セルは行をまたげない
/// - クォートなしのセルは前後の空白を取り除く
/// - ヘッダより列数が多い行はエラー、少ない行は空セルで補う
/// </summary>
public static class CsvParser
{
    public static CsvDocument ParseFile(string filePath, DiagnosticBag diagnostics)
        => Parse(filePath, File.ReadAllText(filePath, Encoding.UTF8), diagnostics);

    public static CsvDocument Parse(string filePath, string content, DiagnosticBag diagnostics)
    {
        var fileName = Path.GetFileName(filePath);
        if (content.Length > 0 && content[0] == (char)0xFEFF)
        {
            content = content[1..];
        }

        var lines = content.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
        string[]? header = null;
        var headerLine = 0;
        var rows = new List<CsvRow>();

        for (var i = 0; i < lines.Length; i++)
        {
            var lineNumber = i + 1;
            var line = lines[i];
            if (string.IsNullOrWhiteSpace(line)) continue;
            if (line.TrimStart().StartsWith('#')) continue;

            if (!TrySplitLine(line, out var cells, out var error))
            {
                diagnostics.Error(fileName, lineNumber, null, error!);
                continue;
            }

            if (header is null)
            {
                header = cells;
                headerLine = lineNumber;
                for (var c = 0; c < header.Length; c++)
                {
                    if (header[c].Length == 0)
                    {
                        diagnostics.Error(fileName, lineNumber, null, $"ヘッダの {c + 1} 列目が空です");
                    }
                }
                var duplicated = header
                    .Where(h => h.Length > 0)
                    .GroupBy(h => h, StringComparer.OrdinalIgnoreCase)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key);
                foreach (var name in duplicated)
                {
                    diagnostics.Error(fileName, lineNumber, name, "ヘッダに同名の列が複数あります");
                }
                continue;
            }

            if (cells.Length > header.Length)
            {
                diagnostics.Error(fileName, lineNumber, null,
                    $"列数がヘッダより多いです (ヘッダ {header.Length} 列、この行 {cells.Length} 列)");
                continue;
            }

            if (cells.Length < header.Length)
            {
                var padded = new string[header.Length];
                Array.Copy(cells, padded, cells.Length);
                for (var c = cells.Length; c < padded.Length; c++) padded[c] = "";
                cells = padded;
            }

            rows.Add(new CsvRow(lineNumber, cells));
        }

        if (header is null)
        {
            diagnostics.Error(fileName, null, null, "ヘッダ行がありません");
            header = Array.Empty<string>();
        }

        return new CsvDocument(filePath, headerLine, header, rows);
    }

    /// <summary>1 行をセルに分割する。RFC 4180 風のクォートに対応(行をまたぐセルは非対応)。</summary>
    public static bool TrySplitLine(string line, out string[] cells, out string? error)
    {
        var result = new List<string>();
        var buffer = new StringBuilder();
        var i = 0;

        while (true)
        {
            buffer.Clear();
            var j = i;
            while (j < line.Length && (line[j] == ' ' || line[j] == '\t')) j++;

            if (j < line.Length && line[j] == '"')
            {
                j++;
                var closed = false;
                while (j < line.Length)
                {
                    var ch = line[j];
                    if (ch == '"')
                    {
                        if (j + 1 < line.Length && line[j + 1] == '"')
                        {
                            buffer.Append('"');
                            j += 2;
                            continue;
                        }
                        closed = true;
                        j++;
                        break;
                    }
                    buffer.Append(ch);
                    j++;
                }

                if (!closed)
                {
                    cells = Array.Empty<string>();
                    error = "引用符が閉じられていません";
                    return false;
                }

                while (j < line.Length && (line[j] == ' ' || line[j] == '\t')) j++;
                if (j < line.Length && line[j] != ',')
                {
                    cells = Array.Empty<string>();
                    error = "引用符で囲まれたセルの後に余分な文字があります";
                    return false;
                }
                result.Add(buffer.ToString());
            }
            else
            {
                var end = line.IndexOf(',', i);
                var raw = end < 0 ? line[i..] : line[i..end];
                result.Add(raw.Trim());
                j = end < 0 ? line.Length : end;
            }

            if (j >= line.Length) break;

            i = j + 1;
            if (i == line.Length)
            {
                result.Add("");
                break;
            }
        }

        cells = result.ToArray();
        error = null;
        return true;
    }
}
