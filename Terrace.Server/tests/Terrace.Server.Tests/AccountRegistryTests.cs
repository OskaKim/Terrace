using Terrace.Server.Accounts;
using Terrace.Server.Rooms;
using Terrace.Shared;

namespace Terrace.Server.Tests;

public class AccountRegistryTests
{
    [Fact]
    public void トークンなしのログインは毎回新しいキャラクターを作り同じ名前でも別人として扱う()
    {
        var registry = new AccountRegistry();

        var first = registry.Login("alice", null);
        var second = registry.Login("alice", "");
        var guest = registry.Login("  ", null);

        Assert.NotEqual(first.PlayerId, second.PlayerId);
        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal("alice", second.Name);
        Assert.Equal("guest", guest.Name);
        Assert.Equal(3, registry.Count);
    }

    [Fact]
    public void 同じトークンでログインし直すと同じキャラクターに戻り名前も前のまま()
    {
        var registry = new AccountRegistry();
        var alice = registry.Login("alice", null);

        var again = registry.Login("別の名前", alice.Token);

        Assert.Same(alice, again);
        Assert.Equal("alice", again.Name);
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void 知らないトークンでは新しいキャラクターと新しいトークンになる()
    {
        var registry = new AccountRegistry();
        var alice = registry.Login("alice", null);

        var stranger = registry.Login("bob", "0123456789abcdef");

        Assert.NotEqual(alice.PlayerId, stranger.PlayerId);
        Assert.NotEqual("0123456789abcdef", stranger.Token);
        Assert.Equal(AccountRegistry.TokenBytes * 2, stranger.Token.Length);
    }

    [Fact]
    public void トークンからキャラクターを引け空や知らないトークンはnull()
    {
        var registry = new AccountRegistry();
        var alice = registry.Login("alice", null);

        Assert.Same(alice, registry.FindByToken(alice.Token));
        Assert.Null(registry.FindByToken(null));
        Assert.Null(registry.FindByToken(""));
        Assert.Null(registry.FindByToken(alice.Token.ToUpperInvariant() + "x"));
        Assert.Equal(new PlayerInfo { PlayerId = alice.PlayerId, Name = "alice" }.ToString(), alice.ToPlayerInfo().ToString());
    }

    [Fact]
    public void ログインの結果にはトークンとワールドのIDと最初のマップが入る()
    {
        var registry = new AccountRegistry();
        var alice = registry.Login("alice", null);

        var result = AccountRegistry.ToResult(alice, "world-1");

        Assert.Equal(alice.PlayerId, result.PlayerId);
        Assert.Equal(alice.Token, result.Token);
        Assert.Equal("world-1", result.WorldId);
        Assert.Equal("alice", result.Character.Name);
        Assert.Equal(AccountRegistry.StartMapId, result.Character.MapId);
    }

    [Fact]
    public void 長すぎる名前は上限で切り詰める()
    {
        var account = new AccountRegistry().Login(new string('a', 40), null);

        Assert.Equal(AccountRegistry.MaxNameLength, account.Name.Length);
    }

    [Fact]
    public void 参加時の位置が他のプレイヤーへの通知とスナップショットに使われる()
    {
        var sink = new RecordingEventSink();
        var room = new Room(1, null, Array.Empty<EnemySpawnConfig>(), new NullMoveValidator(), sink);
        var alice = new PlayerInfo { PlayerId = 1, Name = "alice" };
        var bob = new PlayerInfo { PlayerId = 2, Name = "bob" };

        room.Join(alice, new MoveState { X = 12f, Y = 3f, Facing = Facing.Left });
        var snapshot = room.Join(bob, new MoveState { X = 1f });

        var seen = Assert.Single(snapshot.Players);
        Assert.Equal((1, 12f, 3f, Facing.Left), (seen.Info.PlayerId, seen.State.X, seen.State.Y, seen.State.Facing));
    }
}
