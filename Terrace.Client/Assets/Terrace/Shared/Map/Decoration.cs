namespace Terrace.Map
{
    /// <summary>見た目だけの飾り(家・柵・草木など)。当たり判定は無い。</summary>
    public sealed class Decoration
    {
        public int Id { get; set; }

        /// <summary>絵の名前(クライアントの素材フォルダからの相対パス。例: "Tiles/fence")。</summary>
        public string Sprite { get; set; } = string.Empty;

        public float X { get; set; }
        public float Y { get; set; }

        /// <summary>描画順。小さいほど奥。</summary>
        public int Layer { get; set; } = -10;

        public float Scale { get; set; } = 1f;
        public bool FlipX { get; set; }

        public Position Position => new Position(X, Y);

        public override string ToString() => $"Decoration#{Id} '{Sprite}' ({X}, {Y}) layer={Layer}";
    }
}
