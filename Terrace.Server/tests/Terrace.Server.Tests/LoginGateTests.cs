using Terrace.Server.Accounts;
using Terrace.Shared;

namespace Terrace.Server.Tests;

public class LoginGateTests
{
    private const string Master = "cd710039aaaabbbbccccddddeeeeffff00001111222233334444555566667777";

    private static LoginRequest Request(string password = "", string master = Master, int version = ProtocolVersion.Current)
        => new() { Name = "alice", Password = password, MasterSha256 = master, ProtocolVersion = version };

    [Fact]
    public void 版とマスタが同じで合言葉が要らなければ通す()
    {
        var gate = new LoginGate(Master, "");

        Assert.False(gate.PasswordRequired);
        Assert.Equal(LoginRejection.None, gate.Check(Request()));
        Assert.Equal(LoginRejection.None, gate.Check(Request(password: "何でも")));
    }

    [Fact]
    public void 版が違えば断る()
    {
        var gate = new LoginGate(Master, "");

        Assert.Equal(LoginRejection.ProtocolMismatch, gate.Check(Request(version: ProtocolVersion.Current + 1)));
        Assert.Equal(LoginRejection.ProtocolMismatch, gate.Check(Request(version: 0)));   // 版を送らない古い Client
        Assert.Contains("版が違います", gate.Describe(LoginRejection.ProtocolMismatch, Request(version: 0)));
    }

    [Fact]
    public void マスタが違えば断り送られてこなければ比べない()
    {
        var gate = new LoginGate(Master, "");

        Assert.Equal(LoginRejection.MasterMismatch, gate.Check(Request(master: "ffff0000")));
        Assert.Equal(LoginRejection.None, gate.Check(Request(master: "")));
        Assert.Equal(LoginRejection.None, gate.Check(Request(master: Master.ToUpperInvariant())));
        Assert.Contains("cd710039、", gate.Describe(LoginRejection.MasterMismatch, Request(master: "ffff0000")));
        // 先頭 8 文字が同じなら、見分けが付くように全部を出す
        Assert.Contains(Master, gate.Describe(LoginRejection.MasterMismatch, Request(master: "cd710039" + new string('0', 56))));
    }

    [Fact]
    public void サーバーがマスタを読めていなければマスタは比べない()
    {
        var gate = new LoginGate("", "");

        Assert.Equal(LoginRejection.None, gate.Check(Request(master: "ffff0000")));
    }

    [Fact]
    public void 合言葉の要るワールドでは違う合言葉と空の合言葉を断る()
    {
        var gate = new LoginGate(Master, "terrace");

        Assert.True(gate.PasswordRequired);
        Assert.Equal(LoginRejection.None, gate.Check(Request(password: "terrace")));
        Assert.Equal(LoginRejection.WrongPassword, gate.Check(Request(password: "Terrace")));
        Assert.Equal(LoginRejection.WrongPassword, gate.Check(Request(password: "")));
        Assert.Equal("このワールドに入るには合言葉が要ります", gate.Describe(LoginRejection.WrongPassword, Request(password: "")));
        Assert.Equal("合言葉が違います", gate.Describe(LoginRejection.WrongPassword, Request(password: "x")));
    }

    [Fact]
    public void 版が違うときは合言葉より先に版を理由にする()
    {
        var gate = new LoginGate(Master, "terrace");

        Assert.Equal(LoginRejection.ProtocolMismatch, gate.Check(Request(password: "", version: 0)));
    }

    [Fact]
    public void ワールドのIDは指定が無ければ作りワールドごとに違う()
    {
        var a = new WorldIdentity("テラスの村");
        var b = new WorldIdentity("テラスの村");

        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal("テラスの村", a.Name);
        Assert.Equal("fixed", new WorldIdentity("", "fixed").Id);
        Assert.Equal("Terrace", new WorldIdentity("  ").Name);
    }
}
