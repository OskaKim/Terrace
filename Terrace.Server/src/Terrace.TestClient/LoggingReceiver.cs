using Terrace.Shared;

namespace Terrace.TestClient;

/// <summary>受信したイベントを 1 行ずつ標準出力にログする IGameHubReceiver。</summary>
public sealed class LoggingReceiver(WorldView world) : IGameHubReceiver
{
    public void OnJoin(PlayerInfo player, MoveState state)
    {
        world.Remember(player);
        Log.Write($"[recv] OnJoin player={player.Name} x={state.X:F1} y={state.Y:F1} state={state.Motion}");
    }

    public void OnLeave(int playerId)
    {
        Log.Write($"[recv] OnLeave player={world.NameOf(playerId)}");
        world.Forget(playerId);
    }

    public void OnMove(int playerId, MoveState state)
    {
        Log.Write($"[recv] OnMove player={world.NameOf(playerId)} x={state.X:F1} y={state.Y:F1} state={state.Motion}");
    }

    public void OnEnemySpawn(EnemyState enemy)
    {
        world.Update(enemy);
        Log.Write($"[recv] OnEnemySpawn enemy#{enemy.InstanceId} id={enemy.EnemyId} hp={enemy.Hp}/{enemy.MaxHp} at ({enemy.X:F1}, {enemy.Y:F1})");
    }

    public void OnEnemyDamaged(int enemyInstanceId, int hp, int attackerPlayerId, int damage)
    {
        world.UpdateEnemyHp(enemyInstanceId, hp, isDead: false);
        Log.Write($"[recv] OnEnemyDamaged enemy#{enemyInstanceId} hp={hp} by={world.NameOf(attackerPlayerId)} damage={damage}");
    }

    public void OnEnemyDead(int enemyInstanceId, int killerPlayerId, int[] droppedItemIds)
    {
        world.UpdateEnemyHp(enemyInstanceId, 0, isDead: true);
        Log.Write($"[recv] OnEnemyDead enemy#{enemyInstanceId} killer={world.NameOf(killerPlayerId)} drops=[{string.Join(", ", droppedItemIds)}]");
    }

    public void OnSnapshot(RoomSnapshot snapshot)
    {
        Log.Write($"[recv] OnSnapshot map={snapshot.MapId} players={snapshot.Players.Length} enemies={snapshot.Enemies.Length}");
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
