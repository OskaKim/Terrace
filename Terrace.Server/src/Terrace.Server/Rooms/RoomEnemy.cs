using Terrace.Shared;

namespace Terrace.Server.Rooms;

/// <summary>ルーム内の敵 1 体(サーバー権威)。</summary>
public sealed class RoomEnemy
{
    public RoomEnemy(int instanceId, EnemySpawnConfig spawn)
    {
        InstanceId = instanceId;
        EnemyId = spawn.EnemyId;
        SpawnPointId = spawn.SpawnPointId;
        X = spawn.X;
        Y = spawn.Y;
        MaxHp = spawn.MaxHp;
        Hp = spawn.MaxHp;
        RespawnSeconds = spawn.RespawnSeconds;
        DropItemIds = spawn.DropItemIds;
        DropRate = spawn.DropRate;
    }

    public int InstanceId { get; }
    public int EnemyId { get; }
    public int SpawnPointId { get; }
    public float X { get; }
    public float Y { get; }
    public int MaxHp { get; }
    public float RespawnSeconds { get; }
    public int[] DropItemIds { get; }
    public float DropRate { get; }

    public int Hp { get; internal set; }
    public bool IsDead { get; internal set; }

    /// <summary>死亡中の残り復活秒数。</summary>
    public float RespawnTimer { get; internal set; }

    internal void Respawn()
    {
        Hp = MaxHp;
        IsDead = false;
        RespawnTimer = 0f;
    }

    public EnemyState ToState() => new()
    {
        InstanceId = InstanceId,
        EnemyId = EnemyId,
        X = X,
        Y = Y,
        Hp = Hp,
        MaxHp = MaxHp,
        IsDead = IsDead,
    };

    public override string ToString() => ToState().ToString();
}
