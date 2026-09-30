namespace Terrace.Map
{
    /// <summary>マップに立つ NPC。Kind が "shop" なら ShopId の店を開く。</summary>
    public sealed class Npc
    {
        public const string KindTalk = "talk";
        public const string KindShop = "shop";

        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public float X { get; set; }
        public float Y { get; set; }

        /// <summary>"talk"(話すだけ)または "shop"(店)。</summary>
        public string Kind { get; set; } = KindTalk;

        /// <summary>Kind が "shop" のときの店の ID。</summary>
        public string ShopId { get; set; } = string.Empty;

        /// <summary>話しかけたときの一言。</summary>
        public string Greeting { get; set; } = string.Empty;

        /// <summary>見た目の名前(クライアント側で解釈する。空なら既定)。</summary>
        public string Sprite { get; set; } = string.Empty;

        public Position Position => new Position(X, Y);
        public bool IsShop => Kind == KindShop;

        public override string ToString() => $"Npc#{Id} '{Name}' ({X}, {Y}) {Kind}{(IsShop ? $" shop={ShopId}" : "")}";
    }
}
