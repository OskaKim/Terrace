namespace Terrace.Server.Rooms;

/// <summary>mapId → スポーン設定。</summary>
public interface ISpawnConfigProvider
{
    IEnumerable<EnemySpawnConfig> GetSpawns(int mapId);
}

/// <summary>
/// ダミーのスポーン設定。どのマップにも同じ 2 か所を返す。
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
            X = 25f,
            Y = 5f,
            MaxHp = 30,
            Count = 2,
            RespawnSeconds = 10f,
            DropItemIds = new[] { 1, 4 },
            DropRate = 0.5f,
        };
        yield return new EnemySpawnConfig
        {
            SpawnPointId = 2,
            EnemyId = 2,
            X = 45f,
            Y = 10f,
            MaxHp = 80,
            Count = 1,
            RespawnSeconds = 30f,
            DropItemIds = new[] { 2, 3 },
            DropRate = 0.3f,
        };
    }
}
