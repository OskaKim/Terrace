using Terrace.Server.Chat;
using Terrace.Shared;

namespace Terrace.Server.Tests;

public class ChatGateTests
{
    private double _now;

    private ChatGate Gate(int burst = 3, double refillSeconds = 1) => new(burst, refillSeconds, () => _now);

    [Fact]
    public void ふつうの本文は前後の空白を落として通す()
    {
        var result = Gate().Check(1, "  こんにちは 🙂  ");

        Assert.Equal(ChatOutcome.Accepted, result.Outcome);
        Assert.Equal("こんにちは 🙂", result.Text);
    }

    [Fact]
    public void 制御文字と書式文字を取り除き絵文字のZWJは残す()
    {
        // 改行・タブ・NUL・右から左への上書き(U+202E)・ゼロ幅スペース(U+200B)・行区切り(U+2028)
        Assert.Equal("abcdef", ChatGate.Clean("a\nb\tc\0d\u202Ee\u200Bf\u2028"));
        var family = "👨\u200D👩\u200D👧";
        Assert.Equal(family, ChatGate.Clean(family));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\n\r\t\u202E")]
    public void 空になる本文は何もしない(string? text)
    {
        Assert.Equal(ChatOutcome.Ignored, Gate().Check(1, text).Outcome);
    }

    [Fact]
    public void 上限より長ければ断り上限ちょうどなら通す()
    {
        var gate = Gate();

        Assert.Equal(ChatOutcome.Accepted, gate.Check(1, new string('あ', ChatLimits.MaxLength)).Outcome);
        var tooLong = gate.Check(1, new string('あ', ChatLimits.MaxLength + 1));
        Assert.Equal(ChatOutcome.Rejected, tooLong.Outcome);
        Assert.Equal(ChatRejection.TooLong, tooLong.Reason);
    }

    [Fact]
    public void 長さは取り除いた後で数える()
    {
        var text = new string('あ', ChatLimits.MaxLength) + "\n\n\n   ";

        Assert.Equal(ChatOutcome.Accepted, Gate().Check(1, text).Outcome);
    }

    [Fact]
    public void 続けて送れるのはburst回までで時間が経てば戻る()
    {
        var gate = Gate(burst: 3, refillSeconds: 1);

        for (var i = 0; i < 3; i++) Assert.Equal(ChatOutcome.Accepted, gate.Check(1, $"msg{i}").Outcome);
        var fast = gate.Check(1, "msg3");
        Assert.Equal(ChatOutcome.Rejected, fast.Outcome);
        Assert.Equal(ChatRejection.TooFast, fast.Reason);

        _now += 0.5;
        Assert.Equal(ChatOutcome.Rejected, gate.Check(1, "まだ").Outcome);
        _now += 0.6;
        Assert.Equal(ChatOutcome.Accepted, gate.Check(1, "1 回分戻った").Outcome);
        Assert.Equal(ChatOutcome.Rejected, gate.Check(1, "また尽きた").Outcome);

        _now += 100;
        for (var i = 0; i < 3; i++) Assert.Equal(ChatOutcome.Accepted, gate.Check(1, "たまっても burst まで").Outcome);
        Assert.Equal(ChatOutcome.Rejected, gate.Check(1, "4 回目").Outcome);
    }

    [Fact]
    public void 間引きはプレイヤーごと()
    {
        var gate = Gate(burst: 1, refillSeconds: 10);

        Assert.Equal(ChatOutcome.Accepted, gate.Check(1, "alice").Outcome);
        Assert.Equal(ChatOutcome.Rejected, gate.Check(1, "alice").Outcome);
        Assert.Equal(ChatOutcome.Accepted, gate.Check(2, "bob").Outcome);
    }

    [Fact]
    public void 長すぎる本文と空の本文は送れる回数を減らさない()
    {
        var gate = Gate(burst: 1, refillSeconds: 10);

        gate.Check(1, new string('a', ChatLimits.MaxLength + 1));
        gate.Check(1, "   ");
        Assert.Equal(ChatOutcome.Accepted, gate.Check(1, "ok").Outcome);
    }

    [Fact]
    public void 戻る秒数が0以下なら間引かない()
    {
        var gate = Gate(burst: 1, refillSeconds: 0);

        for (var i = 0; i < 20; i++) Assert.Equal(ChatOutcome.Accepted, gate.Check(1, "連投").Outcome);
    }
}
