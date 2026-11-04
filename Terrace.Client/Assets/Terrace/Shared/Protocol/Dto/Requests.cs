using MessagePack;

namespace Terrace.Shared
{
    /// <summary><see cref="IAccountService.LoginAsync"/> の要求。</summary>
    [MessagePackObject]
    public sealed class LoginRequest
    {
        /// <summary>表示名。新しいキャラクターを作るときだけ使う(トークンで戻るときは、前の名前のまま)。</summary>
        [Key(0)]
        public string Name { get; set; } = string.Empty;

        /// <summary>このワールドで前に受け取ったトークン。初めてなら空(新しいキャラクターを作る)。</summary>
        [Key(1)]
        public string Token { get; set; } = string.Empty;

        /// <summary>ワールドの合言葉。要らないワールドなら空でよい。</summary>
        [Key(2)]
        public string Password { get; set; } = string.Empty;

        /// <summary>Client の通信の定義の版(<see cref="Terrace.Shared.ProtocolVersion.Current"/>)。</summary>
        [Key(3)]
        public int ProtocolVersion { get; set; }

        /// <summary>Client のマスタの sha256。空ならサーバーは比べない(マスタを持たないテストクライアントのため)。</summary>
        [Key(4)]
        public string MasterSha256 { get; set; } = string.Empty;
    }

    /// <summary><see cref="IGameHub.JoinAsync"/> の要求。</summary>
    [MessagePackObject]
    public sealed class JoinRequest
    {
        [Key(0)]
        public int MapId { get; set; }

        // Key(1) は以前の Self(名乗った自分)。使い回さない

        /// <summary>参加した瞬間の位置(他の人にはここに現れる)。</summary>
        [Key(2)]
        public MoveState State { get; set; } = new MoveState();

        /// <summary>ログインで受け取ったトークン。誰が参加するか(ID と名前)は、サーバーがこれから決める。</summary>
        [Key(3)]
        public string Token { get; set; } = string.Empty;
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
