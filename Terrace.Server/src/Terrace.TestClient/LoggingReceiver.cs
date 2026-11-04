using Terrace.Shared;

namespace Terrace.TestClient;

/// <summary>
/// 受信した通知を 1 行ずつ標準出力にログする IGameHubReceiver。敵の位置は数を絞って出す。
/// ここで振り分けていない通知は、型の名前だけを出す(通知を足しても、ここを直さなくてよい)。
/// </summary>
public sealed class LoggingReceiver(WorldView world) : IGameHubReceiver
{
    private int _moveBatches;

    public void OnNotification(INotification notification)
    {
        switch (notification)
        {
            case RoomSnapshot snapshot:
                OnSnapshot(snapshot);
                break;
            case PlayerJoinedNotification joined:
                world.Remember(joined.Player);
                Log.Write($"[recv] PlayerJoined player={joined.Player.Name} x={joined.State.X:F1} y={joined.State.Y:F1} state={joined.State.Motion}");
                break;
            case PlayerLeftNotification left:
                Log.Write($"[recv] PlayerLeft player={world.NameOf(left.PlayerId)}");
                world.Forget(left.PlayerId);
                break;
            case PlayerMovedNotification moved:
                Log.Write($"[recv] PlayerMoved player={world.NameOf(moved.PlayerId)} x={moved.State.X:F1} y={moved.State.Y:F1} state={moved.State.Motion}");
                break;
            case EnemySpawnedNotification spawned:
                world.Update(spawned.Enemy);
                Log.Write($"[recv] EnemySpawned {spawned.Enemy}");
                break;
            case EnemyDamagedNotification damaged:
                world.UpdateEnemyHp(damaged.EnemyInstanceId, damaged.Hp, isDead: false);
                Log.Write($"[recv] EnemyDamaged enemy#{damaged.EnemyInstanceId} hp={damaged.Hp} by={world.NameOf(damaged.AttackerPlayerId)} damage={damaged.Damage}");
                break;
            case EnemyKilledNotification killed:
                world.UpdateEnemyHp(killed.EnemyInstanceId, 0, isDead: true);
                Log.Write($"[recv] EnemyKilled enemy#{killed.EnemyInstanceId} killer={world.NameOf(killed.KillerPlayerId)} drops=[{string.Join(", ", killed.DroppedItemIds)}]");
                break;
            case EnemiesMovedNotification moves:
                OnEnemiesMoved(moves.Enemies);
                break;
            case DropsSpawnedNotification drops:
                foreach (var drop in drops.Drops)
                {
                    world.AddDrop(drop);
                    Log.Write($"[recv] DropSpawned {drop}");
                }
                break;
            case DropRemovedNotification removed:
                world.RemoveDrop(removed.DropId);
                Log.Write($"[recv] DropRemoved drop#{removed.DropId} by={(removed.PlayerId == DropRemovedNotification.Expired ? "(時間切れ)" : world.NameOf(removed.PlayerId))}");
                break;
            default:
                Log.Write($"[recv] {notification.GetType().Name}");
                break;
        }
    }

    private void OnSnapshot(RoomSnapshot snapshot)
    {
        Log.Write($"[recv] RoomSnapshot map={snapshot.MapId} players={snapshot.Players.Length} enemies={snapshot.Enemies.Length} drops={snapshot.Drops.Length}");
        foreach (var player in snapshot.Players)
        {
            world.Remember(player.Info);
            Log.Write($"[recv]   player={player.Info.Name} x={player.State.X:F1} y={player.State.Y:F1} state={player.State.Motion}");
        }
        foreach (var enemy in snapshot.Enemies)
        {
            world.Update(enemy);
            Log.Write($"[recv]   {enemy}");
        }
        foreach (var drop in snapshot.Drops)
        {
            world.AddDrop(drop);
            Log.Write($"[recv]   {drop}");
        }
    }

    private void OnEnemiesMoved(EnemyMoveState[] enemies)
    {
        foreach (var move in enemies) world.UpdateEnemyPosition(move);
        // 0.2 秒ごとに来るので 25 回に 1 回(約 5 秒ごと)だけ出す
        if (_moveBatches++ % 25 == 0 && enemies.Length > 0)
        {
            var first = enemies[0];
            Log.Write($"[recv] EnemiesMoved {enemies.Length} 体 (例: enemy#{first.InstanceId} x={first.X:F1} y={first.Y:F1} {first.Facing})");
        }
    }
}

public static class Log
{
    private static readonly object Gate = new();

    public static void Write(string message)
    {
        lock (Gate)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {message}");
        }
    }
}
