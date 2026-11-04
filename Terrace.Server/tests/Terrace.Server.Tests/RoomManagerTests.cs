using Terrace.Map;
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
            mapLookup: _ => null,
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

    [Fact]
    public void あるルームのTickが例外を投げてもほかのルームは進みTickFailedで知らせる()
    {
        var (manager, sinks) = CreateManager();
        var failed = new List<(int MapId, Exception Error)>();
        manager.TickFailed += (room, error) => failed.Add((room.MapId, error));
        manager.Join(1, Alice);
        manager.Join(2, Bob);
        manager.Find(1)!.Attack(Alice.PlayerId, 1, 999);   // 復活 10 秒
        manager.Find(2)!.Attack(Bob.PlayerId, 1, 999);
        sinks[1].FailOnEnemySpawned = true;

        manager.TickAll(10.5f);

        Assert.Single(sinks[2].Spawned);
        var (mapId, error) = Assert.Single(failed);
        Assert.Equal(1, mapId);
        Assert.IsType<InvalidOperationException>(error);
        Assert.NotNull(manager.Find(1)); // 例外を投げたルームも残す
    }

    [Fact]
    public void 空になって破棄したルームは閉じられ破棄の前に複製した配列からTickされても通知しない()
    {
        var (manager, sinks) = CreateManager();
        manager.Join(1, Alice);
        var room = manager.Find(1)!;
        room.Attack(Alice.PlayerId, 1, 999);

        manager.Leave(1, Alice.PlayerId);
        Assert.True(room.IsClosed);
        var before = sinks[1].Events.Count;

        room.Tick(10.5f);   // 復活の時間を過ぎても、閉じたルームは何もしない

        Assert.Equal(before, sinks[1].Events.Count);
        Assert.Empty(sinks[1].Spawned);
    }

    [Fact]
    public void マップの湧き点とマスタからスポーン設定を組み立てる()
    {
        var map = new MapData
        {
            Id = 7,
            Bounds = new WorldBounds { Left = -5, Right = 45, Top = 20, Bottom = -5 },
            Footholds = new List<Foothold> { new() { Id = 1, X1 = 0, Y1 = 0, X2 = 40, Y2 = 0 } },
            SpawnPoints = new List<SpawnPoint>
            {
                new() { Id = 1, X = 10, Y = 0, EnemyId = 1, RespawnSeconds = 8 },
                new() { Id = 2, X = 30, Y = 0, EnemyId = 99, RespawnSeconds = 0 },
            },
        };
        var provider = new MapSpawnConfigProvider(id => id == 7 ? map : null, new FakeStats());

        var spawns = provider.GetSpawns(7).ToList();

        Assert.Equal(2, spawns.Count);
        Assert.Equal(("Slime", 10, 1, 8f), (spawns[0].Name, spawns[0].MaxHp, spawns[0].Attack, spawns[0].RespawnSeconds));
        Assert.Equal(new[] { 1, 4 }, spawns[0].DropItemIds);
        Assert.Equal(("enemy99", 10, 10f), (spawns[1].Name, spawns[1].MaxHp, spawns[1].RespawnSeconds));
        Assert.Empty(provider.GetSpawns(8));

        var manager = new RoomManager(provider.GetSpawns, id => id == 7 ? map : null, new NullMoveValidator(), _ => new RecordingEventSink());
        var room = manager.GetOrCreate(7);
        Assert.Equal(2, room.Enemies.Count);
        Assert.NotNull(room.Enemies[0].Ground);
        Assert.Equal("Slime", room.Enemies[0].Name);
    }

    private sealed class FakeStats : IEnemyStatsProvider
    {
        public EnemyStats? Get(int enemyId) => enemyId == 1 ? new EnemyStats(1, "Slime", 10, 1, new[] { 1, 4 }) : null;
    }
}
