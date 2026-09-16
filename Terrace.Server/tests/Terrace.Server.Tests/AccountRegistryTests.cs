using Terrace.Server.Accounts;
using Terrace.Server.Rooms;
using Terrace.Shared;

namespace Terrace.Server.Tests;

public class AccountRegistryTests
{
    [Fact]
    public void ログインのたびに新しいIDを発行し同じ名前でも別人として扱う()
    {
        var registry = new AccountRegistry();

        var first = registry.Login("alice");
        var second = registry.Login("alice");
        var guest = registry.Login("  ");

        Assert.NotEqual(first.PlayerId, second.PlayerId);
        Assert.Equal("alice", second.Character.Name);
        Assert.Equal("guest", guest.Character.Name);
        Assert.Equal(AccountRegistry.StartMapId, first.Character.MapId);
        Assert.Equal(3, registry.Count);
    }

    [Fact]
    public void 長すぎる名前は16文字に切り詰める()
    {
        var result = new AccountRegistry().Login(new string('a', 40));

        Assert.Equal(16, result.Character.Name.Length);
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
