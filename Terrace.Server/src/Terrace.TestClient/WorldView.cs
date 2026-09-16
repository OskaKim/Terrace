using System.Collections.Concurrent;
using Terrace.Shared;

namespace Terrace.TestClient;

/// <summary>受信したイベントから組み立てる、クライアント側のルームの見え方(名前解決と敵・落とし物の位置)。</summary>
public sealed class WorldView
{
    private readonly ConcurrentDictionary<int, string> _playerNames = new();
    private readonly ConcurrentDictionary<int, EnemyState> _enemies = new();
    private readonly ConcurrentDictionary<int, DropState> _drops = new();

    public void Remember(PlayerInfo player) => _playerNames[player.PlayerId] = player.Name;

    public void Forget(int playerId) => _playerNames.TryRemove(playerId, out _);

    public string NameOf(int playerId) => _playerNames.TryGetValue(playerId, out var name) ? name : $"player{playerId}";

    public void Update(EnemyState enemy) => _enemies[enemy.InstanceId] = enemy;

    public void UpdateEnemyHp(int instanceId, int hp, bool isDead)
    {
        if (_enemies.TryGetValue(instanceId, out var enemy))
        {
            enemy.Hp = hp;
            enemy.IsDead = isDead;
        }
    }

    public void UpdateEnemyPosition(EnemyMoveState move)
    {
        if (_enemies.TryGetValue(move.InstanceId, out var enemy))
        {
            enemy.X = move.X;
            enemy.Y = move.Y;
            enemy.Facing = move.Facing;
        }
    }

    public void AddDrop(DropState drop) => _drops[drop.DropId] = drop;

    public void RemoveDrop(int dropId) => _drops.TryRemove(dropId, out _);

    public IReadOnlyCollection<EnemyState> Enemies => _enemies.Values.ToArray();

    public IReadOnlyCollection<DropState> Drops => _drops.Values.ToArray();

    /// <summary>生きている敵のうち最も近いもの。</summary>
    public EnemyState? NearestAliveEnemy(float x, float y)
    {
        EnemyState? best = null;
        var bestDistance = float.MaxValue;
        foreach (var enemy in _enemies.Values)
        {
            if (enemy.IsDead) continue;
            var dx = enemy.X - x;
            var dy = enemy.Y - y;
            var distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance < bestDistance)
            {
                best = enemy;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>最も近い落とし物。</summary>
    public DropState? NearestDrop(float x, float y)
    {
        DropState? best = null;
        var bestDistance = float.MaxValue;
        foreach (var drop in _drops.Values)
        {
            var dx = drop.X - x;
            var dy = drop.Y - y;
            var distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance < bestDistance)
            {
                best = drop;
                bestDistance = distance;
            }
        }
        return best;
    }
}
