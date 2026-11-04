using System.Diagnostics;
using System.Globalization;
using System.Text;
using Terrace.Shared;

namespace Terrace.Server.Chat;

/// <summary>チャットの本文を受けたときの結果。</summary>
public enum ChatOutcome
{
    /// <summary>配る(<see cref="ChatResult.Text"/> は取り除いた後の本文)。</summary>
    Accepted,

    /// <summary>取り除いたら空になった。何もしない。</summary>
    Ignored,

    /// <summary>断る(<see cref="ChatResult.Reason"/>)。送った本人にだけ知らせる。</summary>
    Rejected,
}

public readonly record struct ChatResult(ChatOutcome Outcome, string Text, ChatRejection Reason);

/// <summary>
/// チャットの規則(純 C#。MagicOnion を知らない)。ワールド全体のチャットなので、ルーム(<c>Rooms/</c>)の外に置く。
///
/// 1. 前後の空白を落とし、制御文字(改行を含む)と書式文字(右から左への上書きなど)を取り除く。絵文字をつなぐ ZWJ だけは残す
/// 2. 空になったら何もしない
/// 3. <see cref="ChatLimits.MaxLength"/> より長ければ断る
/// 4. 連投の間引き: プレイヤーごとのトークンバケツ。続けて <c>burst</c> 回まで送れ、<c>refillSeconds</c> ごとに 1 回分戻る。尽きていれば断る
/// </summary>
public sealed class ChatGate
{
    private const char ZeroWidthJoiner = '\u200D';

    private readonly object _gate = new();
    private readonly Dictionary<int, (double Tokens, double At)> _buckets = new();
    private readonly int _burst;
    private readonly double _refillSeconds;
    private readonly Func<double> _clock;

    /// <param name="burst">続けて送れる回数(1 以上に丸める)。</param>
    /// <param name="refillSeconds">1 回分が戻る秒数(0 以下なら間引かない)。</param>
    /// <param name="clock">今の時刻(秒)。テストで差し替える。</param>
    public ChatGate(int burst, double refillSeconds, Func<double>? clock = null)
    {
        _burst = Math.Max(1, burst);
        _refillSeconds = refillSeconds;
        _clock = clock ?? (() => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
    }

    public ChatResult Check(int playerId, string? text)
    {
        var cleaned = Clean(text);
        if (cleaned.Length == 0) return new ChatResult(ChatOutcome.Ignored, cleaned, default);
        if (cleaned.Length > ChatLimits.MaxLength) return new ChatResult(ChatOutcome.Rejected, cleaned, ChatRejection.TooLong);
        if (!TryConsume(playerId)) return new ChatResult(ChatOutcome.Rejected, cleaned, ChatRejection.TooFast);
        return new ChatResult(ChatOutcome.Accepted, cleaned, default);
    }

    /// <summary>制御文字と書式文字(ZWJ を除く)を取り除き、前後の空白を落とす。</summary>
    public static string Clean(string? text)
    {
        if (string.IsNullOrEmpty(text)) return string.Empty;
        var builder = new StringBuilder(text.Length);
        foreach (var c in text)
        {
            var category = char.GetUnicodeCategory(c);
            if (category is UnicodeCategory.Control or UnicodeCategory.LineSeparator or UnicodeCategory.ParagraphSeparator) continue;
            if (category == UnicodeCategory.Format && c != ZeroWidthJoiner) continue;
            builder.Append(c);
        }
        return builder.ToString().Trim();
    }

    private bool TryConsume(int playerId)
    {
        if (_refillSeconds <= 0) return true;
        var now = _clock();
        lock (_gate)
        {
            var (tokens, at) = _buckets.TryGetValue(playerId, out var bucket) ? bucket : (_burst, now);
            tokens = Math.Min(_burst, tokens + (now - at) / _refillSeconds);
            if (tokens < 1)
            {
                _buckets[playerId] = (tokens, now);
                return false;
            }
            _buckets[playerId] = (tokens - 1, now);
            return true;
        }
    }
}
