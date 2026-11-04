using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Terrace.Client.Presentation;
using Terrace.Map;

namespace Terrace.Client.Tests.EditMode
{
    /// <summary>ミニマップの見せ方の計算(MinimapLayout)を、Unity なしで確かめる。</summary>
    [TestFixture]
    public class MinimapLayoutTests
    {
        private const float Margin = 2f;
        private const float PixelsPerUnit = 5f;

        /// <summary>境界は広いが、中身は x 0〜40・y 0〜12 に収まるマップ。出現地点・ポータル・NPC を 1 つずつ。</summary>
        private static MapData Town()
        {
            return new MapData
            {
                Id = 9,
                Name = "Town",
                Bounds = new WorldBounds { Left = -20, Right = 80, Top = 40, Bottom = -10 },
                Footholds = new List<Foothold>
                {
                    new Foothold { Id = 1, X1 = 0, Y1 = 0, X2 = 30, Y2 = 0, NextId = 2 },
                    new Foothold { Id = 2, X1 = 30, Y1 = 0, X2 = 40, Y2 = 5, PrevId = 1 },
                    new Foothold { Id = 3, X1 = 10, Y1 = 8, X2 = 20, Y2 = 8, Layer = 1 },
                    new Foothold { Id = 4, X1 = 40, Y1 = 5, X2 = 40, Y2 = 12 },
                },
                Ladders = new List<Ladder> { new Ladder { Id = 1, X = 12, Y1 = 0, Y2 = 8 } },
                Portals = new List<Portal>
                {
                    new Portal { Id = 1, Name = "spawn", X = 5, Y = 0, Kind = PortalKind.Spawn },
                    new Portal { Id = 2, Name = "east", X = 38, Y = 4, TargetMapId = 2, TargetPortalName = "west" },
                },
                Npcs = new List<Npc> { new Npc { Id = 1, Name = "メリー", X = 20, Y = 0 } },
            };
        }

        private static MinimapLayout Create(MapData map, float maxWidth = 1000f, float maxHeight = 1000f)
            => MinimapLayout.Create(map, Margin, PixelsPerUnit, maxWidth, maxHeight);

        [Test]
        public void 描く範囲は境界ではなく中身を囲む範囲に余白を足したもの()
        {
            var layout = Create(Town());

            Assert.AreEqual((-2f, -2f, 42f, 14f), (layout.Left, layout.Bottom, layout.Right, layout.Top));
        }

        [Test]
        public void 中身の無いマップは境界を描く()
        {
            var map = new MapData { Id = 1, Bounds = new WorldBounds { Left = 0, Right = 20, Top = 10, Bottom = 0 } };

            var layout = Create(map);

            Assert.AreEqual((-2f, -2f, 22f, 12f), (layout.Left, layout.Bottom, layout.Right, layout.Top));
        }

        [Test]
        public void 収まるときは1ユニットあたりの画素数で描く()
        {
            var layout = Create(Town());

            Assert.AreEqual(PixelsPerUnit, layout.Scale);
            Assert.AreEqual(44f * PixelsPerUnit, layout.Width, 1e-3f);
            Assert.AreEqual(16f * PixelsPerUnit, layout.Height, 1e-3f);
        }

        [TestCase(110f, 1000f)]
        [TestCase(1000f, 40f)]
        [TestCase(110f, 40f)]
        public void 大きすぎるときは縦横比を保って最大の大きさに収める(float maxWidth, float maxHeight)
        {
            var layout = Create(Town(), maxWidth, maxHeight);

            Assert.LessOrEqual(layout.Width, maxWidth + 1e-3f);
            Assert.LessOrEqual(layout.Height, maxHeight + 1e-3f);
            Assert.AreEqual(44f / 16f, layout.Width / layout.Height, 1e-3f, "縦横比");
            Assert.IsTrue(System.Math.Abs(layout.Width - maxWidth) < 1e-3f || System.Math.Abs(layout.Height - maxHeight) < 1e-3f, "どちらかが最大に届く");
        }

        [Test]
        public void 範囲の四隅がミニマップの四隅に写る()
        {
            var layout = Create(Town());

            Assert.AreEqual((0f, 0f), layout.ToMinimap(layout.Left, layout.Bottom));
            Assert.AreEqual((layout.Width, layout.Height), layout.ToMinimap(layout.Right, layout.Top));
            Assert.AreEqual((0f, layout.Height), layout.ToMinimap(layout.Left, layout.Top));
            Assert.AreEqual((layout.Width, 0f), layout.ToMinimap(layout.Right, layout.Bottom));
            Assert.AreEqual((10f, 10f), layout.ToMinimap(0f, 0f), "原点は余白の分だけ内側");
        }

        [Test]
        public void 範囲の外の点は縁に寄せる()
        {
            var layout = Create(Town());

            Assert.AreEqual((0f, layout.Height), layout.ToMinimap(-100f, 100f), "左上の外");
            Assert.AreEqual((layout.Width, 0f), layout.ToMinimap(100f, -100f), "右下の外");
            Assert.AreEqual((layout.ToMinimap(20f, 0f).X, layout.Height), layout.ToMinimap(20f, 50f), "高く跳んだときは上の縁に、X はそのまま");
        }

        [Test]
        public void 点の一覧はNPCと入れるポータルで出現地点は含まない()
        {
            var layout = Create(Town());

            Assert.AreEqual(2, layout.Markers.Count);
            var portal = layout.Markers.Single(m => m.Kind == MinimapMarkerKind.Portal);
            Assert.AreEqual(layout.ToMinimap(38f, 4f), (portal.X, portal.Y));
            var npc = layout.Markers.Single(m => m.Kind == MinimapMarkerKind.Npc);
            Assert.AreEqual(layout.ToMinimap(20f, 0f), (npc.X, npc.Y));
        }

        [Test]
        public void 線の一覧は壁を含む足場とはしご()
        {
            var layout = Create(Town());

            Assert.AreEqual(4, layout.Lines.Count(l => l.Kind == MinimapLineKind.Foothold));
            var ladder = layout.Lines.Single(l => l.Kind == MinimapLineKind.Ladder);
            Assert.AreEqual(ladder.X1, ladder.X2, "はしごは縦の線");
            Assert.AreEqual((layout.ToMinimap(12f, 0f).Y, layout.ToMinimap(12f, 8f).Y), (ladder.Y1, ladder.Y2));
            var slope = layout.Lines.First(l => l.Kind == MinimapLineKind.Foothold && l.Y1 != l.Y2 && l.X1 != l.X2);
            Assert.AreEqual(layout.ToMinimap(30f, 0f), (slope.X1, slope.Y1));
            Assert.AreEqual(layout.ToMinimap(40f, 5f), (slope.X2, slope.Y2));
        }
    }
}
