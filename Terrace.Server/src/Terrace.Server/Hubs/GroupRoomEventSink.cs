using Cysharp.Runtime.Multicast;
using Terrace.Server.Rooms;
using Terrace.Shared;

namespace Terrace.Server.Hubs;

/// <summary>Room の通知をグループ配信に変換する。ルーム破棄時に Dispose され、グループも閉じる。</summary>
public sealed class GroupRoomEventSink(IMulticastSyncGroup<int, IGameHubReceiver> group) : IRoomEventSink, IDisposable
{
    public void OnPlayerJoined(PlayerInfo player, MoveState state) => group.Except(player.PlayerId).OnJoin(player, state);

    public void OnPlayerLeft(int playerId) => group.Except(playerId).OnLeave(playerId);

    public void OnPlayerMoved(int playerId, MoveState state) => group.Except(playerId).OnMove(playerId, state);

    public void OnEnemySpawned(EnemyState enemy) => group.All.OnEnemySpawn(enemy);

    public void OnEnemyDamaged(int enemyInstanceId, int hp, int attackerPlayerId, int damage)
        => group.All.OnEnemyDamaged(enemyInstanceId, hp, attackerPlayerId, damage);

    public void OnEnemyDead(int enemyInstanceId, int killerPlayerId, int[] droppedItemIds)
        => group.All.OnEnemyDead(enemyInstanceId, killerPlayerId, droppedItemIds);

    public void OnEnemyMoved(IReadOnlyList<EnemyMoveState> enemies) => group.All.OnEnemyMove(enemies.ToArray());

    public void OnDropSpawned(IReadOnlyList<DropState> drops) => group.All.OnDropSpawn(drops.ToArray());

    public void OnDropRemoved(int dropId, int playerId) => group.All.OnDropRemoved(dropId, playerId);

    public void Dispose() => group.Dispose();
}
