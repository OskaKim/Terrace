using MessagePack;

namespace Terrace.Shared
{
    /// <summary>プレイヤーの識別情報。</summary>
    [MessagePackObject]
    public sealed class PlayerInfo
    {
        [Key(0)]
        public int PlayerId { get; set; }

        [Key(1)]
        public string Name { get; set; } = string.Empty;

        public override string ToString() => $"{Name}#{PlayerId}";
    }
}
