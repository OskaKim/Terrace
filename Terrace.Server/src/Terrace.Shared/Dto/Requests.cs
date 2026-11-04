using MessagePack;

namespace Terrace.Shared
{
    /// <summary><see cref="IAccountService.LoginAsync"/> の要求。</summary>
    [MessagePackObject]
    public sealed class LoginRequest
    {
        /// <summary>表示名。</summary>
        [Key(0)]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary><see cref="IGameHub.JoinAsync"/> の要求。</summary>
    [MessagePackObject]
    public sealed class JoinRequest
    {
        [Key(0)]
        public int MapId { get; set; }

        /// <summary>ログインで発行された自分。</summary>
        [Key(1)]
        public PlayerInfo Self { get; set; } = new PlayerInfo();

        /// <summary>参加した瞬間の位置(他の人にはここに現れる)。</summary>
        [Key(2)]
        public MoveState State { get; set; } = new MoveState();
    }

    /// <summary><see cref="IGameHub.Attack"/> の要求。</summary>
    [MessagePackObject]
    public sealed class AttackRequest
    {
        [Key(0)]
        public int EnemyInstanceId { get; set; }

        [Key(1)]
        public int Damage { get; set; }
    }

    /// <summary><see cref="IGameHub.Pickup"/> の要求。</summary>
    [MessagePackObject]
    public sealed class PickupRequest
    {
        [Key(0)]
        public int DropId { get; set; }
    }
}
