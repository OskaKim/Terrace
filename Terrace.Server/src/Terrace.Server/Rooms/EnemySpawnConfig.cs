namespace Terrace.Server.Rooms;

/// <summary>湧き点 1 つ分のスポーン設定。将来はマップデータ(SpawnPoint)とマスタ(Enemy)から組み立てる。</summary>
public sealed class EnemySpawnConfig
{
    public int SpawnPointId { get; init; }
    public int EnemyId { get; init; }
    public float X { get; init; }
    public float Y { get; init; }
    public int MaxHp { get; init; } = 10;

    /// <summary>この湧き点から同時に湧かせる数。</summary>
    public int Count { get; init; } = 1;

    /// <summary>死亡後に復活するまでの秒数。</summary>
    public float RespawnSeconds { get; init; } = 10f;

    /// <summary>ドロップ候補のアイテム ID。</summary>
    public int[] DropItemIds { get; init; } = Array.Empty<int>();

    /// <summary>候補 1 つあたりのドロップ確率(0〜1)。</summary>
    public float DropRate { get; init; } = 0.5f;
}
