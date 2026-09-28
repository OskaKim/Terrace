using System;
using Terrace.Map;

namespace Terrace.Client.Core
{
    /// <summary>敵の種類ごとの定義。マスタデータ(enemy.csv)から組み立てる。</summary>
    public sealed class EnemyDefinition
    {
        public int EnemyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int MaxHp { get; set; } = 10;
        public int Attack { get; set; } = 1;
        public int[] DropItemIds { get; set; } = Array.Empty<int>();

        /// <summary>ドロップ候補 1 つあたりの確率(0〜1)。</summary>
        public float DropRate { get; set; } = 0.5f;

        public float MoveSpeed { get; set; } = 1.5f;
        public float Width { get; set; } = 1.0f;
        public float Height { get; set; } = 1.0f;

        /// <summary>倒したときに得るメソ。0 以下なら MaxHp と同じ。</summary>
        public int MesoReward { get; set; }

        public int EffectiveMesoReward => MesoReward > 0 ? MesoReward : MaxHp;
    }

    /// <summary>ルーム内の敵 1 体。</summary>
    public sealed class EnemyEntity
    {
        public EnemyEntity(int instanceId, EnemyDefinition definition, SpawnPoint spawnPoint, Foothold? ground)
        {
            InstanceId = instanceId;
            Definition = definition;
            SpawnPoint = spawnPoint;
            Ground = ground;
            X = spawnPoint.X;
            Y = ground?.GetYAt(spawnPoint.X) ?? spawnPoint.Y;
            NetX = X;
            NetY = Y;
            MaxHp = definition.MaxHp;
            Hp = definition.MaxHp;
            Facing = Direction.Left;
        }

        public int InstanceId { get; }
        public EnemyDefinition Definition { get; }
        public SpawnPoint SpawnPoint { get; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public Direction Facing { get; internal set; }
        public int Hp { get; internal set; }

        /// <summary>最大 HP。オフラインではマスタの値、オンラインではサーバーが送ってきた値。</summary>
        public int MaxHp { get; internal set; }

        public bool IsDead { get; internal set; }
        public float RespawnTimer { get; internal set; }
        public Foothold? Ground { get; internal set; }

        /// <summary>被弾表示の残り秒数。</summary>
        public float HitFlash { get; internal set; }

        /// <summary>オンライン: サーバーから届いた最新の位置。表示位置(X, Y)はここへ滑らかに寄せる。</summary>
        public float NetX { get; internal set; }
        public float NetY { get; internal set; }

        public bool IsAlive => !IsDead;
        public float HpRatio => MaxHp <= 0 ? 0f : (float)Hp / MaxHp;

        public override string ToString() => $"{Definition.Name}#{InstanceId} hp={Hp}/{MaxHp} ({X:F1}, {Y:F1}){(IsDead ? " dead" : "")}";
    }

    /// <summary>地面に落ちているアイテム。</summary>
    public sealed class ItemDrop
    {
        public ItemDrop(int dropId, int itemId, float x, float y, float lifetimeSeconds)
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
    }

    /// <summary>プレイヤーの攻撃の結果。</summary>
    public readonly struct AttackOutcome
    {
        public static readonly AttackOutcome Miss = default;

        public AttackOutcome(EnemyEntity target, int damage, bool killed, int[] droppedItemIds, bool pending = false)
        {
            Target = target;
            Damage = damage;
            Killed = killed;
            DroppedItemIds = droppedItemIds;
            Pending = pending;
        }

        /// <summary>オンライン: 当たった相手をサーバーへ送っただけで、HP の増減と撃破の結果はまだ届いていない。</summary>
        public bool Pending { get; }

        public EnemyEntity? Target { get; }
        public int Damage { get; }
        public bool Killed { get; }
        public int[] DroppedItemIds { get; }
        public bool Hit => Target != null;
    }
}
