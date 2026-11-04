using System.Collections.Generic;
using System.Text;

namespace Terrace.Client.Presentation
{
    /// <summary>吹き出しの設定。値はここを正とする。</summary>
    public sealed class ChatBubbleConfig
    {
        /// <summary>吹き出しを出しておく秒数。</summary>
        public float Seconds { get; set; } = 5f;

        /// <summary>1 行の幅(半角を 1、全角と絵文字を 2 と数える)。これを超えたら折り返す。</summary>
        public int WrapColumns { get; set; } = 24;

        /// <summary>行数の上限。超えた分は切って「…」を付ける。</summary>
        public int MaxLines { get; set; } = 3;

        public static ChatBubbleConfig Default => new ChatBubbleConfig();
    }

    /// <summary>吹き出しの文字の折り返し(純 C#)。幅は半角を 1、全角と絵文字を 2 として数える。</summary>
    public static class ChatBubbleLayout
    {
        public const string Ellipsis = "…";

        /// <summary>text を maxColumns の幅で折り返し、maxLines 行までにする。超えたら最後の行の末尾を「…」にする。</summary>
        public static IReadOnlyList<string> Wrap(string text, int maxColumns, int maxLines)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(text) || maxColumns <= 0 || maxLines <= 0) return lines;

            var line = new StringBuilder();
            var columns = 0;
            for (var i = 0; i < text.Length; i++)
            {
                // 絵文字などのサロゲートペアは割らない
                var length = char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]) ? 2 : 1;
                var piece = text.Substring(i, length);
                var width = WidthOf(piece);
                if (columns + width > maxColumns && line.Length > 0)
                {
                    lines.Add(line.ToString());
                    line.Clear();
                    columns = 0;
                }
                line.Append(piece);
                columns += width;
                i += length - 1;
            }
            if (line.Length > 0) lines.Add(line.ToString());

            if (lines.Count <= maxLines) return lines;
            lines.RemoveRange(maxLines, lines.Count - maxLines);
            lines[maxLines - 1] = Truncate(lines[maxLines - 1], maxColumns - WidthOf(Ellipsis)) + Ellipsis;
            return lines;
        }

        /// <summary>幅(半角を 1、全角と絵文字を 2)。</summary>
        public static int WidthOf(string text)
        {
            var width = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    width += 2;
                    i++;
                    continue;
                }
                width += IsWide(text[i]) ? 2 : 1;
            }
            return width;
        }

        private static string Truncate(string line, int maxColumns)
        {
            var builder = new StringBuilder();
            var columns = 0;
            for (var i = 0; i < line.Length; i++)
            {
                var length = char.IsHighSurrogate(line[i]) && i + 1 < line.Length && char.IsLowSurrogate(line[i + 1]) ? 2 : 1;
                var piece = line.Substring(i, length);
                var width = WidthOf(piece);
                if (columns + width > maxColumns) break;
                builder.Append(piece);
                columns += width;
                i += length - 1;
            }
            return builder.ToString();
        }

        /// <summary>全角として数える文字(CJK・かな・全角記号・ハングルなど)。厳密な東アジアの幅ではなく、吹き出しの大きさの目安。</summary>
        private static bool IsWide(char c) => c >= '\u1100' && (c <= '\u115F' || (c >= '\u2E80' && c <= '\uA4CF') || (c >= '\uAC00' && c <= '\uD7A3') || (c >= '\uF900' && c <= '\uFAFF') || (c >= '\uFE30' && c <= '\uFE4F') || (c >= '\uFF00' && c <= '\uFF60') || (c >= '\uFFE0' && c <= '\uFFE6'));
    }
}
