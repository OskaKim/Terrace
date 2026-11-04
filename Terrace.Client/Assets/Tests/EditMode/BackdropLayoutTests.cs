using NUnit.Framework;
using Terrace.Client.Presentation;
using Terrace.Map;

namespace Terrace.Client.Tests.EditMode
{
    /// <summary>背景の層の位置の計算(BackdropLayout)を、Unity なしで確かめる。</summary>
    [TestFixture]
    public class BackdropLayoutTests
    {
        // 草原(map 1)と同じ大きさのマップ: 幅 90、中心 (40, 11)
        private const float CenterX = 40f;
        private const float CenterY = 11f;
        private const float MapLeft = -5f;
        private const float MapRight = 85f;

        // 今のカメラ(OrthographicSize 7、16:9)の画面の半分の幅
        private const float HalfViewWidth = 7f * 16f / 9f;

        private static (float X, float Y) Anchor(BackgroundLayer layer, float cameraX, float cameraY, float time = 0f)
            => (BackdropLayout.AnchorX(layer, CenterX, cameraX, time), BackdropLayout.AnchorY(layer, CenterY, cameraY));

        [TestCase(0f)]
        [TestCase(0.3f)]
        [TestCase(1f)]
        [TestCase(1.25f)]
        public void カメラがマップの中心にいるとき層は置いた位置に来る(float parallax)
        {
            var layer = new BackgroundLayer { X = 3f, Y = -2f, ParallaxX = parallax, ParallaxY = parallax };

            Assert.AreEqual((CenterX + 3f, CenterY - 2f), Anchor(layer, CenterX, CenterY));
        }

        [Test]
        public void 係数が1なら層はワールドに固定される()
        {
            var layer = new BackgroundLayer { X = 3f, Y = -2f, ParallaxX = 1f, ParallaxY = 1f };

            Assert.AreEqual(Anchor(layer, CenterX, CenterY), Anchor(layer, 10f, 1.5f));
            Assert.AreEqual(Anchor(layer, CenterX, CenterY), Anchor(layer, 70f, 14.5f));
        }

        [Test]
        public void 係数が0なら層は画面に固定される()
        {
            var layer = new BackgroundLayer { X = 3f, Y = -2f, ParallaxX = 0f, ParallaxY = 0f };

            foreach (var (cameraX, cameraY) in new[] { (10f, 1.5f), (40f, 11f), (70f, 14.5f) })
            {
                var (x, y) = Anchor(layer, cameraX, cameraY);
                Assert.AreEqual(3f, x - cameraX, 1e-4f);
                Assert.AreEqual(-2f, y - cameraY, 1e-4f);
            }
        }

        [Test]
        public void 係数の分だけ画面の上を動く()
        {
            var far = new BackgroundLayer { ParallaxX = 0.3f, ParallaxY = 0.5f };
            var front = new BackgroundLayer { ParallaxX = 1.3f, ParallaxY = 1f };

            // カメラが右へ 10、上へ 4 動いたとき、画面の上では足場が左へ 10、下へ 4 動く。層はその係数倍
            Assert.AreEqual(-3f, ScreenMove(far, 10f, 0f).X, 1e-4f);
            Assert.AreEqual(-2f, ScreenMove(far, 0f, 4f).Y, 1e-4f);
            Assert.AreEqual(-13f, ScreenMove(front, 10f, 0f).X, 1e-4f, "前景は足場より速く流れる");
            Assert.AreEqual(-4f, ScreenMove(front, 0f, 4f).Y, 1e-4f);
        }

        private static (float X, float Y) ScreenMove(BackgroundLayer layer, float dx, float dy)
        {
            var before = Anchor(layer, 20f, 2f);
            var after = Anchor(layer, 20f + dx, 2f + dy);
            return (after.X - before.X - dx, after.Y - before.Y - dy);
        }

        [Test]
        public void 流れる層は時間とともに横へ進む()
        {
            var layer = new BackgroundLayer { ParallaxX = 0.2f, ScrollX = 0.5f };

            var start = Anchor(layer, 30f, 2f, time: 0f).X;
            Assert.AreEqual(start + 5f, Anchor(layer, 30f, 2f, time: 10f).X, 1e-4f);
            Assert.AreEqual(start, Anchor(new BackgroundLayer { ParallaxX = 0.2f }, 30f, 2f, time: 10f).X, 1e-4f, "ScrollX が 0 なら流れない");
        }

        [TestCase(0.08f, 30f)]
        [TestCase(0.5f, 1.4f)]
        [TestCase(1.3f, 0.7f)]
        public void 隙間なく並べる層はカメラが動ける範囲のどこでも画面の横幅を覆う(float parallax, float spriteWidth)
        {
            var layer = new BackgroundLayer { X = 1.7f, ParallaxX = parallax, TileX = true };
            var period = BackdropLayout.Period(layer, spriteWidth);
            var maxCount = BackdropLayout.MaxTileCount(period, spriteWidth, HalfViewWidth);
            Assert.AreEqual(spriteWidth, period, 1e-4f);

            // カメラの X は、境界のクランプで [左 + 画面の半分, 右 − 画面の半分] を動く
            for (var cameraX = MapLeft + HalfViewWidth; cameraX <= MapRight - HalfViewWidth; cameraX += 0.37f)
            {
                var anchorX = BackdropLayout.AnchorX(layer, CenterX, cameraX, 0f);
                var tiles = BackdropLayout.VisibleTiles(anchorX, period, spriteWidth, cameraX, HalfViewWidth);
                var left = anchorX + tiles.First * period - spriteWidth * 0.5f;
                var right = anchorX + (tiles.First + tiles.Count - 1) * period + spriteWidth * 0.5f;

                Assert.LessOrEqual(left, cameraX - HalfViewWidth + 1e-3f, $"camera {cameraX}: 左端 {tiles}");
                Assert.GreaterOrEqual(right, cameraX + HalfViewWidth - 1e-3f, $"camera {cameraX}: 右端 {tiles}");
                Assert.LessOrEqual(tiles.Count, maxCount, $"camera {cameraX}: 数 {tiles}");
            }
        }

        [Test]
        public void 間を空けて並べる層は画面に掛かる絵だけを返す()
        {
            var layer = new BackgroundLayer { TileX = true, SpacingX = 8f };
            const float width = 2f;
            var period = BackdropLayout.Period(layer, width);
            Assert.AreEqual(10f, period, 1e-4f);

            // 絵の中心は 0, ±10, ±20 …。画面 [-12.4, 12.4] に掛かるのは -10, 0, 10(-20 の右端 -19 と 20 の左端 19 は外)
            var tiles = BackdropLayout.VisibleTiles(0f, period, width, 0f, 12.4f);
            Assert.AreEqual((-1, 3), (tiles.First, tiles.Count));

            // 端に少しでも掛かれば描く: 画面 [-10.9, 13.9] には -10(右端 -9)・0・10 が掛かる
            tiles = BackdropLayout.VisibleTiles(0f, period, width, 1.5f, 12.4f);
            Assert.AreEqual((-1, 3), (tiles.First, tiles.Count));
            tiles = BackdropLayout.VisibleTiles(0f, period, width, 7f, 12.4f);
            Assert.AreEqual((0, 3), (tiles.First, tiles.Count), "画面 [-5.4, 19.4] には 0, 10, 20 が掛かる");
            Assert.LessOrEqual(tiles.Count, BackdropLayout.MaxTileCount(period, width, 12.4f));
        }

        [Test]
        public void 幅の無い絵でも並べる数は膨らまない()
        {
            var layer = new BackgroundLayer { TileX = true };
            var period = BackdropLayout.Period(layer, 0f);

            Assert.AreEqual(BackdropLayout.MinPeriod, period);
            Assert.Greater(BackdropLayout.MaxTileCount(period, 0f, 1f), 0);
        }
    }
}
