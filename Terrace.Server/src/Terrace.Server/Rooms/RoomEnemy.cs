using Terrace.Map;
using Terrace.Shared;

namespace Terrace.Server.Rooms;

/// <summary>ルーム内の敵 1 体(サーバー権威)。自分の足場の上を端で折り返しながら巡回する。</summary>
public sealed class RoomEnemy
{
    public RoomEnemy(int instanceId, EnemySpawnConfig spawn, Foothold? ground)
    {
        InstanceId = instanceId;
        EnemyId = spawn.EnemyId;
        Name = spawn.Name;
        SpawnPointId = spawn.SpawnPointId;
        SpawnX = spawn.X;
        SpawnY = ground?.GetYAt(spawn.X) ?? spawn.Y;
        Ground = ground;
        X = SpawnX;
        Y = SpawnY;
        MaxHp = spawn.MaxHp;
        Hp = spawn.MaxHp;
        Attack = spawn.Attack;
        RespawnSeconds = spawn.RespawnSeconds;
        DropItemIds = spawn.DropItemIds;
        DropRate = spawn.DropRate;
        MoveSpeed = spawn.MoveSpeed;
    }

    public int InstanceId { get; }
    public int EnemyId { get; }
    public string Name { get; }
    public int SpawnPointId { get; }
    public float SpawnX { get; }
    public float SpawnY { get; }
    public Foothold? Ground { get; }
    public int MaxHp { get; }
    public int Attack { get; }
    public float RespawnSeconds { get; }
    public int[] DropItemIds { get; }
    public float DropRate { get; }
    public float MoveSpeed { get; }

    /// <summary>巡回できるか(足場があり、速さが正)。動かない敵は位置を配信しない。</summary>
    public bool CanMove => Ground != null && Ground.Width > 0f && MoveSpeed > 0f;

    public float X { get; internal set; }
    public float Y { get; internal set; }
    public Facing Facing { get; internal set; } = Facing.Left;
    public int Hp { get; internal set; }
    public bool IsDead { get; internal set; }

    /// <summary>死亡中の残り復活秒数。</summary>
    public float RespawnTimer { get; internal set; }

    internal void Respawn()
    {
        Hp = MaxHp;
        IsDead = false;
        RespawnTimer = 0f;
        X = SpawnX;
        Y = SpawnY;
    }

    /// <summary>足場の上を巡回する。端で折り返す(落ちない)。</summary>
    internal void Patrol(float deltaSeconds)
    {
        if (!CanMove) return;
        var ground = Ground!;

        var direction = Facing == Facing.Right ? 1f : -1f;
        var x = X + direction * MoveSpeed * deltaSeconds;
        if (x >= ground.Right)
        {
            x = ground.Right;
            Facing = Facing.Left;
        }
        else if (x <= ground.Left)
        {
            x = ground.Left;
            Facing = Facing.Right;
        }

        X = x;
        Y = ground.GetYAt(x);
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
        Facing = Facing,
    };

    public EnemyMoveState ToMoveState() => new() { InstanceId = InstanceId, X = X, Y = Y, Facing = Facing };

    public override string ToString() => ToState().ToString();
}

/// <summary>ルーム内に落ちているアイテム(サーバー権威)。</summary>
public sealed class RoomDrop
{
    public RoomDrop(int dropId, int itemId, float x, float y, float lifetimeSeconds)
    {
        DropId = dropId;
        ItemId = itemId;
        X = x;
        Y = y;
        RemainingSeconds = lifetimeSeconds;
    }

    public int DropId { get; }
    public int ItemId { get; }
    public float X { get; }
    public float Y { get; }
    public float RemainingSeconds { get; internal set; }

    public DropState ToState() => new() { DropId = DropId, ItemId = ItemId, X = X, Y = Y };
}
