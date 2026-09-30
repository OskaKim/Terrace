using Terrace.Server.Rooms;
using Terrace.Shared;

namespace Terrace.Server.Tests;

/// <summary>Room からの通知を記録するテスト用の Sink。</summary>
public sealed class RecordingEventSink : IRoomEventSink, IDisposable
{
    public List<string> Events { get; } = new();
    public List<(int PlayerId, MoveState State)> Moves { get; } = new();
    public List<EnemyState> Spawned { get; } = new();
    public List<IReadOnlyList<EnemyMoveState>> EnemyMoves { get; } = new();
    public List<DropState> DropsSpawned { get; } = new();
    public List<(int DropId, int PlayerId)> DropsRemoved { get; } = new();
    public bool Disposed { get; private set; }

    public void OnPlayerJoined(PlayerInfo player, MoveState state) => Events.Add($"Joined:{player.PlayerId}");

    public void OnPlayerLeft(int playerId) => Events.Add($"Left:{playerId}");

    public void OnPlayerMoved(int playerId, MoveState state)
    {
        Events.Add($"Moved:{playerId}:{state.X}");
        Moves.Add((playerId, state));
    }

    public void OnEnemySpawned(EnemyState enemy)
    {
        Events.Add($"EnemySpawn:{enemy.InstanceId}");
        Spawned.Add(enemy);
    }

    public void OnEnemyDamaged(int enemyInstanceId, int hp, int attackerPlayerId, int damage)
        => Events.Add($"EnemyDamaged:{enemyInstanceId}:{hp}:{attackerPlayerId}:{damage}");

    public void OnEnemyDead(int enemyInstanceId, int killerPlayerId, int[] droppedItemIds)
        => Events.Add($"EnemyDead:{enemyInstanceId}:{killerPlayerId}:[{string.Join(",", droppedItemIds)}]");

    public void OnEnemyMoved(IReadOnlyList<EnemyMoveState> enemies)
    {
        Events.Add($"EnemyMoved:{enemies.Count}");
        EnemyMoves.Add(enemies);
    }

    public void OnDropSpawned(IReadOnlyList<DropState> drops)
    {
        Events.Add($"DropSpawned:{drops.Count}");
        DropsSpawned.AddRange(drops);
    }

    public void OnDropRemoved(int dropId, int playerId)
    {
        Events.Add($"DropRemoved:{dropId}:{playerId}");
        DropsRemoved.Add((dropId, playerId));
    }

    public void Dispose() => Disposed = true;
}
