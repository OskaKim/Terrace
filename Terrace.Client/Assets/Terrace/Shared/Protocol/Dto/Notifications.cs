using System;
using MessagePack;

namespace Terrace.Shared
{
    /// <summary>
    /// Server → Client の通知。<see cref="IGameHubReceiver.OnNotification"/> の 1 本で、どの通知も届く。
    /// 受け取った側は型で振り分ける。
    ///
    /// 通知を足すときは、DTO を 1 つ作って、ここに <see cref="UnionAttribute"/> を 1 行足す。
    /// Union の番号は <see cref="KeyAttribute"/> と同じく、付け替えない・使い回さない。足すときは末尾の次の番号にする。
    /// </summary>
    [Union(0, typeof(RoomSnapshot))]
    [Union(1, typeof(PlayerJoinedNotification))]
    [Union(2, typeof(PlayerLeftNotification))]
    [Union(3, typeof(PlayerMovedNotification))]
    [Union(4, typeof(EnemySpawnedNotification))]
    [Union(5, typeof(EnemyDamagedNotification))]
    [Union(6, typeof(EnemyKilledNotification))]
    [Union(7, typeof(EnemiesMovedNotification))]
    [Union(8, typeof(DropsSpawnedNotification))]
    [Union(9, typeof(DropRemovedNotification))]
    public interface INotification
    {
    }

    /// <summary>誰かがマップに参加した(本人以外へ)。</summary>
    [MessagePackObject]
    public sealed class PlayerJoinedNotification : INotification
    {
        [Key(0)]
        public PlayerInfo Player { get; set; } = new PlayerInfo();

        /// <summary>参加した瞬間の位置(ここに現れる)。</summary>
        [Key(1)]
        public MoveState State { get; set; } = new MoveState();
    }

    /// <summary>誰かがマップから退出・切断した(本人以外へ)。</summary>
    [MessagePackObject]
    public sealed class PlayerLeftNotification : INotification
    {
        [Key(0)]
        public int PlayerId { get; set; }
    }

    /// <summary>誰かの移動が受け入れられた(本人以外へ)。</summary>
    [MessagePackObject]
    public sealed class PlayerMovedNotification : INotification
    {
        [Key(0)]
        public int PlayerId { get; set; }

        [Key(1)]
        public MoveState State { get; set; } = new MoveState();
    }

    /// <summary>敵が復活した。最初に湧いた分は <see cref="RoomSnapshot"/> に入っていて、これは届かない。</summary>
    [MessagePackObject]
    public sealed class EnemySpawnedNotification : INotification
    {
        [Key(0)]
        public EnemyState Enemy { get; set; } = new EnemyState();
    }

    /// <summary>攻撃が敵に当たった。</summary>
    [MessagePackObject]
    public sealed class EnemyDamagedNotification : INotification
    {
        [Key(0)]
        public int EnemyInstanceId { get; set; }

        /// <summary>当たった後の HP。</summary>
        [Key(1)]
        public int Hp { get; set; }

        [Key(2)]
        public int AttackerPlayerId { get; set; }

        [Key(3)]
        public int Damage { get; set; }
    }

    /// <summary>敵が倒れた。落とし物があれば、この前に <see cref="DropsSpawnedNotification"/> が届く。</summary>
    [MessagePackObject]
    public sealed class EnemyKilledNotification : INotification
    {
        [Key(0)]
        public int EnemyInstanceId { get; set; }

        [Key(1)]
        public int KillerPlayerId { get; set; }

        /// <summary>落としたアイテムの ID。</summary>
        [Key(2)]
        public int[] DroppedItemIds { get; set; } = Array.Empty<int>();
    }

    /// <summary>動く生きた敵の位置(定期配信)。</summary>
    [MessagePackObject]
    public sealed class EnemiesMovedNotification : INotification
    {
        [Key(0)]
        public EnemyMoveState[] Enemies { get; set; } = Array.Empty<EnemyMoveState>();
    }

    /// <summary>敵がアイテムを落とした。</summary>
    [MessagePackObject]
    public sealed class DropsSpawnedNotification : INotification
    {
        [Key(0)]
        public DropState[] Drops { get; set; } = Array.Empty<DropState>();
    }

    /// <summary>落ちていたアイテムが消えた。</summary>
    [MessagePackObject]
    public sealed class DropRemovedNotification : INotification
    {
        /// <summary>時間切れで消えたときの <see cref="PlayerId"/>。</summary>
        public const int Expired = 0;

        [Key(0)]
        public int DropId { get; set; }

        /// <summary>拾った人。時間切れなら <see cref="Expired"/>。</summary>
        [Key(1)]
        public int PlayerId { get; set; }
    }
}
