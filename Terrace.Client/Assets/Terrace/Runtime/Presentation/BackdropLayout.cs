using System;
using Terrace.Map;

namespace Terrace.Client.Presentation
{
    /// <summary>
    /// 背景の層(<see cref="BackgroundLayer"/>)を、画面のどこに置くかの計算。UnityEngine を使わない(.NET のテストで確かめる)。
    ///
    /// 層の位置 = マップの中心 + (X, Y) + (カメラ位置 − マップの中心) × (1 − Parallax) + ScrollX × 経過秒。
    /// 位置は絵の原点(素材の足元の中央)。TileX の層は、この位置から <see cref="Period"/> おきに絵を並べ、画面に掛かる分だけを描く。
    /// </summary>
    public static class BackdropLayout
    {
        /// <summary>繰り返しの間隔の下限(幅 0 の絵で並べる数が膨らまないように)。</summary>
        public const float MinPeriod = 0.01f;

        public static float AnchorX(BackgroundLayer layer, float mapCenterX, float cameraX, float time)
            => mapCenterX + layer.X + (cameraX - mapCenterX) * (1f - layer.ParallaxX) + layer.ScrollX * time;

        public static float AnchorY(BackgroundLayer layer, float mapCenterY, float cameraY)
            => mapCenterY + layer.Y + (cameraY - mapCenterY) * (1f - layer.ParallaxY);

        /// <summary>TileX の層で絵を並べる間隔 = 絵の幅(拡大後)+ SpacingX。</summary>
        public static float Period(BackgroundLayer layer, float spriteWidth) => Math.Max(MinPeriod, spriteWidth + layer.SpacingX);

        /// <summary>
        /// 画面の横幅 [cameraX − halfViewWidth, cameraX + halfViewWidth] に掛かる絵の番号の範囲。
        /// 番号 i の絵の中心は anchorX + i × period、幅は spriteWidth。
        /// </summary>
        public static TileRange VisibleTiles(float anchorX, float period, float spriteWidth, float cameraX, float halfViewWidth)
        {
            var half = spriteWidth * 0.5f;
            var first = (int)Math.Ceiling((cameraX - halfViewWidth - half - anchorX) / period);
            var last = (int)Math.Floor((cameraX + halfViewWidth + half - anchorX) / period);
            return new TileRange(first, Math.Max(0, last - first + 1));
        }

        /// <summary>画面の横幅に同時に掛かる絵の数の上限。</summary>
        public static int MaxTileCount(float period, float spriteWidth, float halfViewWidth)
            => (int)Math.Ceiling((halfViewWidth * 2f + spriteWidth) / period) + 1;
    }

    /// <summary>並べる絵の番号の範囲 [First, First + Count)。</summary>
    public readonly struct TileRange
    {
        public TileRange(int first, int count)
        {
            First = first;
            Count = count;
        }

        public int First { get; }
        public int Count { get; }

        public override string ToString() => $"[{First}, {First + Count})";
    }
}
