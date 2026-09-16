using Terrace.Map;

namespace Terrace.Server.Rooms;

/// <summary>mapId → スポーン設定。</summary>
public interface ISpawnConfigProvider
{
    IEnumerable<EnemySpawnConfig> GetSpawns(int mapId);
}

/// <summary>敵のマスタ(HP・攻撃・ドロップ)。</summary>
public interface IEnemyStatsProvider
{
    EnemyStats? Get(int enemyId);
}

public sealed record EnemyStats(int EnemyId, string Name, int Hp, int Attack, int[] DropItemIds);

/// <summary>
/// マップの湧き点(SpawnPoint)とマスタの敵(Enemy)からスポーン設定を組み立てる。
/// マスタに無い敵 ID は既定値(HP 10)で湧かせる。
/// </summary>
public sealed class MapSpawnConfigProvider : ISpawnConfigProvider
{
    private readonly Func<int, MapData?> _mapLookup;
    private readonly IEnemyStatsProvider _stats;

    public MapSpawnConfigProvider(Func<int, MapData?> mapLookup, IEnemyStatsProvider stats)
    {
        _mapLookup = mapLookup;
        _stats = stats;
    }

    public IEnumerable<EnemySpawnConfig> GetSpawns(int mapId)
    {
        var map = _mapLookup(mapId);
        if (map == null) yield break;

        foreach (var spawnPoint in map.SpawnPoints)
        {
            var stats = _stats.Get(spawnPoint.EnemyId);
            yield return new EnemySpawnConfig
            {
                SpawnPointId = spawnPoint.Id,
                EnemyId = spawnPoint.EnemyId,
                Name = stats?.Name ?? $"enemy{spawnPoint.EnemyId}",
                X = spawnPoint.X,
                Y = spawnPoint.Y,
                MaxHp = stats?.Hp ?? 10,
                Attack = stats?.Attack ?? 1,
                Count = 1,
                RespawnSeconds = spawnPoint.RespawnSeconds > 0 ? spawnPoint.RespawnSeconds : 10f,
                DropItemIds = stats?.DropItemIds ?? Array.Empty<int>(),
                DropRate = 0.5f,
                MoveSpeed = 1.5f,
            };
        }
    }
}

/// <summary>
/// ダミーのスポーン設定。どのマップにも同じ 2 か所を返す(マップもマスタも無いときの保険)。
/// 座標は Terrace.Map の samples/sample_map.json の湧き点に合わせてある。
/// </summary>
public sealed class DummySpawnConfigProvider : ISpawnConfigProvider
{
    public IEnumerable<EnemySpawnConfig> GetSpawns(int mapId)
    {
        yield return new EnemySpawnConfig
        {
            SpawnPointId = 1,
            EnemyId = 1,
            Name = "Slime",
            X = 25f,
            Y = 5f,
            MaxHp = 30,
            Count = 2,
            RespawnSeconds = 10f,
            DropItemIds = new[] { 1, 4 },
            DropRate = 0.5f,
            MoveSpeed = 0f,
        };
        yield return new EnemySpawnConfig
        {
            SpawnPointId = 2,
            EnemyId = 2,
            Name = "Goblin",
            X = 45f,
            Y = 10f,
            MaxHp = 80,
            Count = 1,
            RespawnSeconds = 30f,
            DropItemIds = new[] { 2, 3 },
            DropRate = 0.3f,
            MoveSpeed = 0f,
        };
    }
}
