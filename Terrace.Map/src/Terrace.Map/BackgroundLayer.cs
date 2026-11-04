namespace Terrace.Map
{
    /// <summary>
    /// 背景の 1 層(見た目だけ)。何層も重ね、層ごとに違う速さで動かして奥行きを出す。
    ///
    /// 位置: マップの中心 + (X, Y) + (カメラ位置 − マップの中心) × (1 − Parallax)。
    /// カメラがマップの中心にいるとき、層は「マップの中心 + (X, Y)」に見える。
    /// </summary>
    public sealed class BackgroundLayer
    {
        /// <summary>絵の名前(クライアントの素材フォルダからの相対パス。例: "Backgrounds/bg_grasslands")。</summary>
        public string Sprite { get; set; } = string.Empty;

        /// <summary>マップの中心からのずれ(X)。</summary>
        public float X { get; set; }

        /// <summary>マップの中心からのずれ(Y)。</summary>
        public float Y { get; set; }

        /// <summary>足場に比べて、画面の上を横にどれだけの速さで動くか。0 = 画面に貼り付く(空)、1 = 足場と同じ、1 より大きい = 手前の前景。</summary>
        public float ParallaxX { get; set; }

        /// <summary>ParallaxX の縦の版。</summary>
        public float ParallaxY { get; set; }

        /// <summary>描く順番。負は背景、正はキャラクターより手前の前景。</summary>
        public int Layer { get; set; } = -100;

        public float Scale { get; set; } = 1f;

        /// <summary>横に繰り返し、画面の横幅を覆う。</summary>
        public bool TileX { get; set; }

        /// <summary>TileX のとき、絵と絵の間に空ける幅(ユニット)。</summary>
        public float SpacingX { get; set; }

        /// <summary>カメラと関係なく横へ流れる速さ(ユニット / 秒)。</summary>
        public float ScrollX { get; set; }

        public override string ToString() => $"BackgroundLayer '{Sprite}' parallax=({ParallaxX}, {ParallaxY}) layer={Layer}";
    }
}
