namespace Terrace.Server.Rooms;

/// <summary>湧き点 1 つ分のスポーン設定。マップの SpawnPoint とマスタの Enemy から組み立てる。</summary>
public sealed class EnemySpawnConfig
{
    public int SpawnPointId { get; init; }
    public int EnemyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public float X { get; init; }
    public float Y { get; init; }
    public int MaxHp { get; init; } = 10;
    public int Attack { get; init; } = 1;

    /// <summary>この湧き点から同時に湧かせる数。</summary>
    public int Count { get; init; } = 1;

    /// <summary>死亡後に復活するまでの秒数。</summary>
    public float RespawnSeconds { get; init; } = 10f;

    /// <summary>ドロップ候補のアイテム ID。</summary>
    public int[] DropItemIds { get; init; } = Array.Empty<int>();

    /// <summary>候補 1 つあたりのドロップ確率(0〜1)。</summary>
    public float DropRate { get; init; } = 0.5f;

    /// <summary>巡回の速さ(ユニット/秒)。0 なら動かない。</summary>
    public float MoveSpeed { get; init; } = 1.5f;
}
