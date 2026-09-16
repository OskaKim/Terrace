using System;
using MessagePack;

namespace Terrace.Shared
{
    /// <summary>ルーム内のプレイヤー 1 人の現在状態。</summary>
    [MessagePackObject]
    public sealed class PlayerSnapshot
    {
        [Key(0)]
        public PlayerInfo Info { get; set; } = new PlayerInfo();

        [Key(1)]
        public MoveState State { get; set; } = new MoveState();
    }

    /// <summary>参加時に受け取る、ルームの全状態。</summary>
    [MessagePackObject]
    public sealed class RoomSnapshot
    {
        [Key(0)]
        public int MapId { get; set; }

        /// <summary>自分以外のプレイヤー。</summary>
        [Key(1)]
        public PlayerSnapshot[] Players { get; set; } = Array.Empty<PlayerSnapshot>();

        [Key(2)]
        public EnemyState[] Enemies { get; set; } = Array.Empty<EnemyState>();

        [Key(3)]
        public DropState[] Drops { get; set; } = Array.Empty<DropState>();
    }
}
