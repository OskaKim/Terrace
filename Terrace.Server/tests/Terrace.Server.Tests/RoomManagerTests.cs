using Terrace.Server.Rooms;
using Terrace.Shared;

namespace Terrace.Server.Tests;

public class RoomManagerTests
{
    private static PlayerInfo Alice => new() { PlayerId = 1, Name = "alice" };
    private static PlayerInfo Bob => new() { PlayerId = 2, Name = "bob" };

    private static (RoomManager Manager, Dictionary<int, RecordingEventSink> Sinks) CreateManager()
    {
        var sinks = new Dictionary<int, RecordingEventSink>();
        var manager = new RoomManager(
            spawnProvider: _ => new DummySpawnConfigProvider().GetSpawns(0),
            moveValidator: new NullMoveValidator(),
            sinkFactory: mapId => sinks[mapId] = new RecordingEventSink());
        return (manager, sinks);
    }

    [Fact]
    public void 同じmapIdなら同じルームを返し違うmapIdなら別のルームになる()
    {
        var (manager, _) = CreateManager();

        var room1 = manager.GetOrCreate(1);
        var again = manager.GetOrCreate(1);
        var room2 = manager.GetOrCreate(2);

        Assert.Same(room1, again);
        Assert.NotSame(room1, room2);
        Assert.Equal(new[] { 1, 2 }, manager.Rooms.Select(r => r.MapId).ToArray());
        Assert.Same(room2, manager.Find(2));
        Assert.Null(manager.Find(3));
    }

    [Fact]
    public void Joinでルームが無ければ作りダミーのスポーン設定で敵を湧かせる()
    {
        var (manager, sinks) = CreateManager();

        var snapshot = manager.Join(1, Alice);

        Assert.Equal(3, snapshot.Enemies.Length);
        Assert.Equal(1, manager.Find(1)!.PlayerCount);
        Assert.Contains("Joined:1", sinks[1].Events);
    }

    [Fact]
    public void 最後のプレイヤーがLeaveするとルームを破棄しSinkをDisposeする()
    {
        var (manager, sinks) = CreateManager();
        var removed = new List<int>();
        manager.RoomRemoved += room => removed.Add(room.MapId);
        manager.Join(1, Alice);
        manager.Join(1, Bob);

        Assert.True(manager.Leave(1, Alice.PlayerId));
        Assert.NotNull(manager.Find(1));
        Assert.False(sinks[1].Disposed);

        Assert.True(manager.Leave(1, Bob.PlayerId));
        Assert.Null(manager.Find(1));
        Assert.True(sinks[1].Disposed);
        Assert.Equal(new[] { 1 }, removed);

        Assert.False(manager.Leave(1, Bob.PlayerId));
    }

    [Fact]
    public void 破棄されたあと再びJoinすると新しいルームとSinkを作る()
    {
        var (manager, sinks) = CreateManager();
        manager.Join(1, Alice);
        var first = manager.Find(1);
        var firstSink = sinks[1];
        manager.Leave(1, Alice.PlayerId);

        manager.Join(1, Alice);

        Assert.NotSame(first, manager.Find(1));
        Assert.NotSame(firstSink, sinks[1]);
        Assert.False(sinks[1].Disposed);
    }

    [Fact]
    public void TickAllが全ルームのリスポーンを進める()
    {
        var (manager, sinks) = CreateManager();
        manager.Join(1, Alice);
        manager.Join(2, Bob);
        manager.Find(1)!.Attack(Alice.PlayerId, 1, 999);   // 復活 10 秒
        manager.Find(2)!.Attack(Bob.PlayerId, 3, 999);     // 復活 30 秒

        manager.TickAll(10.5f);
        Assert.Single(sinks[1].Spawned);
        Assert.Empty(sinks[2].Spawned);

        manager.TickAll(20f);
        Assert.Single(sinks[2].Spawned);
    }
}
