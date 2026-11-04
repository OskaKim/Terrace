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

        Assert.True(first.IsNew);
        Assert.NotEqual(first.Account.PlayerId, second.Account.PlayerId);
        Assert.NotEqual(first.Token, second.Token);
        Assert.Equal("alice", second.Account.Name);
        Assert.Equal("guest", guest.Account.Name);
        Assert.Equal(3, registry.Count);
        Assert.Equal(3, registry.LastPlayerId);
    }

    [Fact]
    public void 同じトークンでログインし直すと同じキャラクターに戻り名前も前のまま()
    {
        var registry = new AccountRegistry();
        var alice = registry.Login("alice", null);

        var again = registry.Login("別の名前", alice.Token);

        Assert.False(again.IsNew);
        Assert.Same(alice.Account, again.Account);
        Assert.Equal(alice.Token, again.Token);
        Assert.Equal("alice", again.Account.Name);
        Assert.Equal(1, registry.Count);
    }

    [Fact]
    public void 知らないトークンでは新しいキャラクターと新しいトークンになる()
    {
        var registry = new AccountRegistry();
        var alice = registry.Login("alice", null);

        var stranger = registry.Login("bob", "0123456789abcdef");

        Assert.True(stranger.IsNew);
        Assert.NotEqual(alice.Account.PlayerId, stranger.Account.PlayerId);
        Assert.NotEqual("0123456789abcdef", stranger.Token);
        Assert.Equal(AccountRegistry.TokenBytes * 2, stranger.Token.Length);
    }

    [Fact]
    public void トークンからキャラクターを引け空や知らないトークンはnull()
    {
        var registry = new AccountRegistry();
        var alice = registry.Login("alice", null);

        Assert.Same(alice.Account, registry.FindByToken(alice.Token));
        Assert.Null(registry.FindByToken(null));
        Assert.Null(registry.FindByToken(""));
        Assert.Null(registry.FindByToken(alice.Token.ToUpperInvariant() + "x"));
    }

    [Fact]
    public void トークンそのものは持たずsha256だけを持つ()
    {
        var registry = new AccountRegistry();
        var alice = registry.Login("alice", null);

        Assert.NotEqual(alice.Token, alice.Account.TokenSha256);
        Assert.Equal(AccountRegistry.HashToken(alice.Token), alice.Account.TokenSha256);
        Assert.Equal(64, alice.Account.TokenSha256.Length);
    }

    [Fact]
    public void 新しいキャラクターを作ったときだけ知らせる()
    {
        var registry = new AccountRegistry();
        var created = new List<string>();
        registry.AccountCreated += account => created.Add(account.Name);

        var alice = registry.Login("alice", null);
        registry.Login("alice", alice.Token);
        registry.Login("bob", null);

        Assert.Equal(new[] { "alice", "bob" }, created);
    }

    [Fact]
    public void 戻したキャラクターにトークンで戻れ次のIDは最大の次から出す()
    {
        var registry = new AccountRegistry();
        var token = "saved-token";
        registry.Restore(new[] { new Account(7, "alice", AccountRegistry.HashToken(token)), new Account(3, "bob", AccountRegistry.HashToken("other")) }, lastPlayerId: 5);

        var again = registry.Login("x", token);
        Assert.False(again.IsNew);
        Assert.Equal(7, again.Account.PlayerId);
        Assert.Equal(8, registry.Login("carol", null).Account.PlayerId);
    }

    [Fact]
    public void ログインの結果にはトークンとワールドのIDと最初のマップが入る()
    {
        var registry = new AccountRegistry();
        var alice = registry.Login("alice", null);

        var result = AccountRegistry.ToResult(alice, "world-1");

        Assert.Equal(alice.Account.PlayerId, result.PlayerId);
        Assert.Equal(alice.Token, result.Token);
        Assert.Equal("world-1", result.WorldId);
        Assert.Equal("alice", result.Character.Name);
        Assert.Equal(AccountRegistry.StartMapId, result.Character.MapId);
    }

    [Fact]
    public void 長すぎる名前は上限で切り詰める()
    {
        var account = new AccountRegistry().Login(new string('a', 40), null).Account;

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
