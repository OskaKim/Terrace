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

        /// <summary>倒した人が得る経験値(マスタの enemy の Exp)。</summary>
        public int Exp { get; set; }

        /// <summary>倒したときに得るメソ。0 以下なら MaxHp と同じ。</summary>
        public int MesoReward { get; set; }

        public int EffectiveMesoReward => MesoReward > 0 ? MesoReward : MaxHp;

        /// <summary>定義が引けない敵 ID の仮の定義。</summary>
        public static EnemyDefinition Placeholder(int enemyId) => new EnemyDefinition { EnemyId = enemyId, Name = $"enemy{enemyId}" };

        /// <summary>lookup で引き、無ければ仮の定義を返す。</summary>
        public static EnemyDefinition Resolve(Func<int, EnemyDefinition?> lookup, int enemyId) => lookup(enemyId) ?? Placeholder(enemyId);
    }

    /// <summary>
    /// 世界にいる敵 1 体。オフラインでもオンラインでも使う値だけを持つ。
    /// 片方の権威でしか使わない値(湧き点・足場・復活待ち、サーバーから届いた位置)は、それぞれの権威(OfflineRoom / RoomMirror)の内側にある。
    /// </summary>
    public sealed class EnemyEntity
    {
        public EnemyEntity(int instanceId, EnemyDefinition definition, float x, float y)
        {
            InstanceId = instanceId;
            Definition = definition;
            X = x;
            Y = y;
            MaxHp = definition.MaxHp;
            Hp = definition.MaxHp;
            Facing = Direction.Left;
        }

        public int InstanceId { get; }
        public EnemyDefinition Definition { get; }
        public float X { get; internal set; }
        public float Y { get; internal set; }
        public Direction Facing { get; internal set; }
        public int Hp { get; internal set; }

        /// <summary>最大 HP。オフラインではマスタの値、オンラインではサーバーが送ってきた値。</summary>
        public int MaxHp { get; internal set; }

        public bool IsDead { get; internal set; }

        /// <summary>被弾表示の残り秒数。</summary>
        public float HitFlash { get; internal set; }

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

    /// <summary>プレイヤーの攻撃(振った結果、当たったかどうか)。HP の増減と撃破は世界の権威の結果のイベントで分かる。</summary>
    public readonly struct AttackOutcome
    {
        public static readonly AttackOutcome Miss = default;

        public AttackOutcome(EnemyEntity target, int damage)
        {
            Target = target;
            Damage = damage;
        }

        public EnemyEntity? Target { get; }
        public int Damage { get; }
        public bool Hit => Target != null;
    }

    /// <summary>世界で起きたことをした人。自分・他のプレイヤー・誰でもない(時間切れなど)のどれか。</summary>
    public readonly struct WorldActor
    {
        /// <summary>誰でもない(落とし物の時間切れなど)。</summary>
        public static readonly WorldActor None = default;

        /// <summary>このクライアントのプレイヤー。オフラインでは、した人は常に自分。</summary>
        public static readonly WorldActor Self = new WorldActor(true, 0);

        private WorldActor(bool isSelf, int playerId)
        {
            IsSelf = isSelf;
            PlayerId = playerId;
        }

        /// <summary>他のプレイヤー(オンライン)。</summary>
        public static WorldActor Other(int playerId) => new WorldActor(false, playerId);

        public bool IsSelf { get; }

        /// <summary>他のプレイヤーなら、その PlayerId。自分と誰でもないときは 0。</summary>
        public int PlayerId { get; }

        public bool IsOther => !IsSelf && PlayerId != 0;

        public override string ToString() => IsSelf ? "self" : IsOther ? $"player{PlayerId}" : "none";
    }

    /// <summary>敵が傷ついた。</summary>
    public readonly struct EnemyDamage
    {
        public EnemyDamage(EnemyEntity enemy, int damage, WorldActor attacker)
        {
            Enemy = enemy;
            Damage = damage;
            Attacker = attacker;
        }

        public EnemyEntity Enemy { get; }
        public int Damage { get; }
        public WorldActor Attacker { get; }
    }

    /// <summary>敵が倒れた。1 体の 1 回の死につき 1 度だけ起きる。</summary>
    public readonly struct EnemyKill
    {
        public EnemyKill(EnemyEntity enemy, WorldActor killer, int[] droppedItemIds)
        {
            Enemy = enemy;
            Killer = killer;
            DroppedItemIds = droppedItemIds;
        }

        public EnemyEntity Enemy { get; }
        public WorldActor Killer { get; }

        /// <summary>落とした品の ItemId。</summary>
        public int[] DroppedItemIds { get; }
    }

    /// <summary>落とし物が消えた。拾われたなら Picker が拾った人、時間切れなら WorldActor.None。</summary>
    public readonly struct DropRemoval
    {
        public DropRemoval(ItemDrop drop, WorldActor picker)
        {
            Drop = drop;
            Picker = picker;
        }

        public ItemDrop Drop { get; }
        public WorldActor Picker { get; }
    }
}
