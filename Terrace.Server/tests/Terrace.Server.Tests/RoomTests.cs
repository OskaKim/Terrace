using Terrace.Server.Rooms;
using Terrace.Shared;

namespace Terrace.Server.Tests;

public class RoomTests
{
    private static PlayerInfo Alice => new() { PlayerId = 1, Name = "alice" };
    private static PlayerInfo Bob => new() { PlayerId = 2, Name = "bob" };

    private static EnemySpawnConfig Slime(int count = 1, float respawnSeconds = 5f, float dropRate = 1f) => new()
    {
        SpawnPointId = 1,
        EnemyId = 1,
        X = 25f,
        Y = 5f,
        MaxHp = 30,
        Count = count,
        RespawnSeconds = respawnSeconds,
        DropItemIds = new[] { 1, 4 },
        DropRate = dropRate,
    };

    private static (Room Room, RecordingEventSink Sink) CreateRoom(params EnemySpawnConfig[] spawns)
    {
        var sink = new RecordingEventSink();
        var room = new Room(1, spawns, new NullMoveValidator(), sink, new Random(42));
        return (room, sink);
    }

    [Fact]
    public void プレイヤー2人がJoinして片方のMoveがもう片方に届く形で状態が更新される()
    {
        var (room, sink) = CreateRoom();
        room.Join(Alice);
        room.Join(Bob);

        var state = new MoveState { X = 120.5f, Y = 64f, VelocityX = 3f, Facing = Facing.Left, Motion = MotionState.Walk };
        var result = room.Move(Alice.PlayerId, state);

        Assert.Equal(MoveResult.Accepted, result);

        // bob へ配信されるイベント(本人以外へ通知する想定)
        var moved = Assert.Single(sink.Moves);
        Assert.Equal(Alice.PlayerId, moved.PlayerId);
        Assert.Equal(120.5f, moved.State.X);
        Assert.Equal(MotionState.Walk, moved.State.Motion);

        // bob から見た alice の現在状態
        var bobView = room.CreateSnapshot(excludePlayerId: Bob.PlayerId);
        var aliceSeenByBob = Assert.Single(bobView.Players);
        Assert.Equal("alice", aliceSeenByBob.Info.Name);
        Assert.Equal(120.5f, aliceSeenByBob.State.X);
        Assert.Equal(64f, aliceSeenByBob.State.Y);
        Assert.Equal(Facing.Left, aliceSeenByBob.State.Facing);
        Assert.Same(state, room.FindPlayer(Alice.PlayerId)!.State);
    }

    [Fact]
    public void Leaveすると他方から見えなくなる()
    {
        var (room, sink) = CreateRoom();
        room.Join(Alice);
        room.Join(Bob);

        Assert.True(room.Leave(Alice.PlayerId));

        Assert.Equal(1, room.PlayerCount);
        Assert.Null(room.FindPlayer(Alice.PlayerId));
        Assert.Empty(room.CreateSnapshot(excludePlayerId: Bob.PlayerId).Players);
        Assert.Contains("Left:1", sink.Events);
        Assert.False(room.Leave(Alice.PlayerId));
        Assert.Equal(MoveResult.UnknownPlayer, room.Move(Alice.PlayerId, new MoveState()));
    }

    [Fact]
    public void 敵にダメージを与えてHPが0になると死亡状態になりTickを進めるとリスポーンする()
    {
        var (room, sink) = CreateRoom(Slime(count: 1, respawnSeconds: 5f, dropRate: 1f));
        room.Join(Alice);
        var enemy = Assert.Single(room.Enemies);

        var first = room.Attack(Alice.PlayerId, enemy.InstanceId, 10);
        Assert.Equal(AttackOutcome.Damaged, first.Outcome);
        Assert.Equal(20, first.Hp);
        Assert.Equal(20, enemy.Hp);
        Assert.Contains($"EnemyDamaged:{enemy.InstanceId}:20:1:10", sink.Events);

        var second = room.Attack(Alice.PlayerId, enemy.InstanceId, 25);
        Assert.Equal(AttackOutcome.Killed, second.Outcome);
        Assert.Equal(0, second.Hp);
        Assert.Equal(new[] { 1, 4 }, second.DroppedItemIds);
        Assert.True(enemy.IsDead);
        Assert.Equal(0, enemy.Hp);
        Assert.Contains($"EnemyDead:{enemy.InstanceId}:1:[1,4]", sink.Events);
        Assert.True(room.CreateSnapshot().Enemies.Single().IsDead);

        // 死亡中は攻撃できない
        Assert.Equal(AttackOutcome.AlreadyDead, room.Attack(Alice.PlayerId, enemy.InstanceId, 5).Outcome);

        // 復活時間に達するまでは死亡のまま
        Assert.Empty(room.Tick(4.9f));
        Assert.True(enemy.IsDead);
        Assert.Empty(sink.Spawned);

        // 達したら復活し、通知される
        var respawned = room.Tick(0.2f);
        Assert.Same(enemy, Assert.Single(respawned));
        Assert.False(enemy.IsDead);
        Assert.Equal(30, enemy.Hp);
        var spawned = Assert.Single(sink.Spawned);
        Assert.Equal(enemy.InstanceId, spawned.InstanceId);
        Assert.Equal(30, spawned.Hp);
        Assert.False(room.CreateSnapshot().Enemies.Single().IsDead);

        // 復活後は再び攻撃できる
        Assert.Equal(AttackOutcome.Damaged, room.Attack(Alice.PlayerId, enemy.InstanceId, 1).Outcome);
    }

    [Fact]
    public void ドロップ率0なら何も落とさない()
    {
        var (room, _) = CreateRoom(Slime(dropRate: 0f));
        room.Join(Alice);
        var enemy = room.Enemies.Single();

        var result = room.Attack(Alice.PlayerId, enemy.InstanceId, 999);

        Assert.Equal(AttackOutcome.Killed, result.Outcome);
        Assert.Empty(result.DroppedItemIds);
    }

    [Fact]
    public void 後からJoinしたプレイヤーのSnapshotに既存プレイヤーと敵の現在状態が含まれる()
    {
        var (room, _) = CreateRoom(Slime(count: 2), new EnemySpawnConfig { SpawnPointId = 2, EnemyId = 2, X = 45f, Y = 10f, MaxHp = 80 });
        room.Join(Alice);
        room.Move(Alice.PlayerId, new MoveState { X = 10f, Y = 5f, Motion = MotionState.Jump });
        room.Attack(Alice.PlayerId, 1, 10);
        room.Attack(Alice.PlayerId, 3, 80);

        var snapshot = room.Join(Bob);

        Assert.Equal(1, snapshot.MapId);
        var alice = Assert.Single(snapshot.Players);
        Assert.Equal("alice", alice.Info.Name);
        Assert.Equal(10f, alice.State.X);
        Assert.Equal(MotionState.Jump, alice.State.Motion);

        Assert.Equal(3, snapshot.Enemies.Length);
        Assert.Equal(new[] { 1, 2, 3 }, snapshot.Enemies.Select(e => e.InstanceId).ToArray());
        Assert.Equal(20, snapshot.Enemies[0].Hp);
        Assert.Equal(30, snapshot.Enemies[1].Hp);
        Assert.True(snapshot.Enemies[2].IsDead);
        Assert.Equal(2, snapshot.Enemies[2].EnemyId);
        Assert.Equal(45f, snapshot.Enemies[2].X);
    }

    [Fact]
    public void 初期スポーンの敵はSnapshotで渡し通知はしない()
    {
        var (room, sink) = CreateRoom(Slime(count: 2), Slime(count: 1));

        Assert.Equal(3, room.Enemies.Count);
        Assert.Equal(new[] { 1, 2, 3 }, room.Enemies.Select(e => e.InstanceId).ToArray());
        Assert.Empty(sink.Spawned);
        Assert.Equal(3, room.CreateSnapshot().Enemies.Length);
    }

    [Fact]
    public void 検証フックが拒否した移動は反映せず通知もしない()
    {
        var sink = new RecordingEventSink();
        var room = new Room(1, Array.Empty<EnemySpawnConfig>(), new RejectFarMoveValidator(), sink);
        room.Join(Alice, new MoveState { X = 0f });

        var result = room.Move(Alice.PlayerId, new MoveState { X = 999f });

        Assert.Equal(MoveResult.Rejected, result);
        Assert.Equal(0f, room.FindPlayer(Alice.PlayerId)!.State.X);
        Assert.Empty(sink.Moves);

        Assert.Equal(MoveResult.Accepted, room.Move(Alice.PlayerId, new MoveState { X = 5f }));
        Assert.Equal(5f, room.FindPlayer(Alice.PlayerId)!.State.X);
    }

    [Fact]
    public void 未知のプレイヤーや存在しない敵は失敗を返す()
    {
        var (room, _) = CreateRoom(Slime());

        Assert.Equal(MoveResult.UnknownPlayer, room.Move(99, new MoveState()));
        Assert.Equal(AttackOutcome.UnknownPlayer, room.Attack(99, 1, 10).Outcome);

        room.Join(Alice);
        Assert.Equal(AttackOutcome.EnemyNotFound, room.Attack(Alice.PlayerId, 42, 10).Outcome);
    }

    [Fact]
    public void 同じPlayerIdで再Joinすると置き換える()
    {
        var (room, sink) = CreateRoom();
        room.Join(Alice, new MoveState { X = 1f });
        room.Join(Alice, new MoveState { X = 2f });

        Assert.Equal(1, room.PlayerCount);
        Assert.Equal(2f, room.FindPlayer(Alice.PlayerId)!.State.X);
        Assert.Equal(2, sink.Events.Count(e => e == "Joined:1"));
    }

    [Fact]
    public void Joinで返るSnapshotに自分は含まれない()
    {
        var (room, _) = CreateRoom();
        var snapshot = room.Join(Alice);

        Assert.Empty(snapshot.Players);
        Assert.Single(room.CreateSnapshot().Players);
    }

    private sealed class RejectFarMoveValidator : IMoveValidator
    {
        public bool IsAcceptable(RoomPlayer player, MoveState proposed) => Math.Abs(proposed.X - player.State.X) <= 10f;
    }
}
