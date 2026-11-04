using MessagePack;

namespace Terrace.Shared
{
    /// <summary>キャラクター情報(最小限)。</summary>
    [MessagePackObject]
    public sealed class CharacterInfo
    {
        [Key(0)]
        public string Name { get; set; } = string.Empty;

        [Key(1)]
        public int Level { get; set; } = 1;

        /// <summary>最後にいたマップ。</summary>
        [Key(2)]
        public int MapId { get; set; }

        [Key(3)]
        public float X { get; set; }

        [Key(4)]
        public float Y { get; set; }
    }

    /// <summary>ログイン結果: プレイヤー ID とキャラ情報、このワールドで使うトークン。</summary>
    [MessagePackObject]
    public sealed class LoginResult
    {
        [Key(0)]
        public int PlayerId { get; set; }

        [Key(1)]
        public CharacterInfo Character { get; set; } = new CharacterInfo();

        /// <summary>このワールドで本人を示すトークン。参加(<see cref="JoinRequest.Token"/>)と、次のログインで送る。</summary>
        [Key(2)]
        public string Token { get; set; } = string.Empty;

        /// <summary>このワールドの ID(<see cref="ServerInfo.WorldId"/>)。Client はトークンをこの ID ごとに覚える。</summary>
        [Key(3)]
        public string WorldId { get; set; } = string.Empty;
    }
}
