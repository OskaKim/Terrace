using MessagePack;

namespace Terrace.Shared
{
    /// <summary>
    /// 通信の定義の版。Client と Server で違えば、ログインで断る。
    /// 通信の定義(Hub・Receiver・DTO・Union)を変えたら、同じ変更で <see cref="Current"/> を 1 上げる。
    /// </summary>
    public static class ProtocolVersion
    {
        public const int Current = 2;
    }

    /// <summary>ログインの前に聞ける、サーバー(ワールド)の情報(<see cref="IAccountService.GetServerInfoAsync"/>)。</summary>
    [MessagePackObject]
    public sealed class ServerInfo
    {
        /// <summary>ワールドを見分ける ID。接続先のアドレスは変わることがあるので、Client はトークンをこの ID ごとに覚える。</summary>
        [Key(0)]
        public string WorldId { get; set; } = string.Empty;

        /// <summary>ワールドの名前(表示用)。</summary>
        [Key(1)]
        public string WorldName { get; set; } = string.Empty;

        /// <summary>サーバーの通信の定義の版(<see cref="Terrace.Shared.ProtocolVersion.Current"/>)。</summary>
        [Key(2)]
        public int ProtocolVersion { get; set; }

        /// <summary>サーバーが使っているマスタ(master.bytes)の sha256。読めていなければ空。</summary>
        [Key(3)]
        public string MasterSha256 { get; set; } = string.Empty;

        /// <summary>入るのに合言葉が要るか。</summary>
        [Key(4)]
        public bool PasswordRequired { get; set; }

        /// <summary>いまマップに参加している人数。</summary>
        [Key(5)]
        public int Players { get; set; }
    }
}
