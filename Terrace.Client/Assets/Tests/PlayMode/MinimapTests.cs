using System.Collections;
using System.Linq;
using NUnit.Framework;
using Terrace.Client.Core;
using Terrace.Client.Presentation;
using Terrace.Client.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace Terrace.Client.Tests.PlayMode
{
    /// <summary>
    /// 右上のミニマップ(MinimapView)。開始すると出て、N で隠したり出したりでき、
    /// 自分の点は更新の間隔ごとに動き、マップを移ると作り直す。
    /// 町・草原・砂の崖の画面を書き出す(Logs/minimap-*.png)。
    /// </summary>
    public class MinimapTests
    {
        [UnityTest]
        public IEnumerator 開始すると右上にミニマップが出てNで隠したり出したりできる()
        {
            var input = new ScriptedInputSource();
            var bootstrap = CreateBootstrap(input, startMapId: 100);
            yield return null; // Start()
            Assert.IsNull(bootstrap.LastError, bootstrap.LastError);
            var sim = bootstrap.Simulation!;
            var minimap = bootstrap.Minimap;

            Assert.IsNotNull(minimap, "ミニマップがある");
            Assert.IsTrue(minimap!.IsVisible, "始めは出ている");
            Assert.AreEqual(sim.Map.Name, minimap.MapName, "見出しはマップ名");
            Assert.IsNotNull(minimap.TerrainTexture, "地形を絵に描いてある");
            var layout = minimap.Layout!;
            var config = MinimapConfig.Default;
            Assert.LessOrEqual(layout.Width, config.MaxWidth + 0.01f);
            Assert.LessOrEqual(layout.Height, config.MaxHeight + 0.01f);
            Assert.AreEqual(sim.Map.Npcs.Count, layout.Markers.Count(m => m.Kind == MinimapMarkerKind.Npc), "NPC の点");
            Assert.AreEqual(sim.Map.Portals.Count(p => !p.IsSpawn), layout.Markers.Count(m => m.Kind == MinimapMarkerKind.Portal), "入れるポータルの点");
            Assert.Less(minimap.ScreenBottom, Screen.height * 0.5f, "画面の上の方に収まる");

            input.MinimapTogglePressed = true;
            yield return null;
            Assert.IsFalse(minimap.IsVisible, "N で隠れる");

            input.MinimapTogglePressed = true;
            yield return null;
            Assert.IsTrue(minimap.IsVisible, "もう一度 N で出る");
            GameSmokeTests.SaveScreenshot(bootstrap, "minimap-town.png");

            Object.Destroy(bootstrap.gameObject);
            yield return null;
            Assert.IsNull(GameObject.Find("MinimapCanvas"), "起動役を消すと、ミニマップも消える");
        }

        [UnityTest]
        public IEnumerator 自分の点は更新の間隔ごとに動きマップを移ると作り直す()
        {
            var input = new ScriptedInputSource();
            var bootstrap = CreateBootstrap(input, startMapId: 1);
            yield return null; // Start()
            Assert.IsNull(bootstrap.LastError, bootstrap.LastError);
            var sim = bootstrap.Simulation!;
            var minimap = bootstrap.Minimap!;
            var config = MinimapConfig.Default;
            Assert.AreEqual("草原", minimap.MapName);

            var start = minimap.SelfDotPosition;
            var expectedStart = minimap.Layout!.ToMinimap(sim.Motor.X, sim.Motor.Y);
            Assert.AreEqual(expectedStart.X, start.x, 1f, "自分の点は自分のいる所");

            // 間隔より短い間は動かない(毎フレームは更新しない)
            minimap.Refresh();
            var beforeTeleport = minimap.SelfDotPosition;
            sim.Motor.Teleport(sim.Motor.X + 10f, sim.Motor.Y);
            yield return null;
            if (Time.unscaledDeltaTime < config.RefreshSeconds * 0.5f)
            {
                Assert.AreEqual(beforeTeleport, minimap.SelfDotPosition, "間隔が経つまでは前の位置のまま");
            }

            // 右へ歩いて止まり、間隔が経てば今の位置に合う
            input.Current = InputFrame.Hold(right: true);
            yield return new WaitForSeconds(0.6f);
            input.Current = InputFrame.None;
            yield return new WaitForSeconds(config.RefreshSeconds * 3f);
            var after = minimap.SelfDotPosition;
            var expected = minimap.Layout!.ToMinimap(sim.Motor.X, sim.Motor.Y);
            Assert.Greater(after.x, start.x + 1f, "右へ動いた分だけ点が動く");
            Assert.AreEqual(expected.X, after.x, 1f);
            Assert.AreEqual(expected.Y, after.y, 1f);
            GameSmokeTests.SaveScreenshot(bootstrap, "minimap-field01.png");

            // マップを移ると作り直す
            var field01Layout = minimap.Layout;
            sim.Travel.ChangeMap(bootstrap.Maps!.Get(2)!);
            Assert.AreEqual("砂の崖", minimap.MapName, "見出しが移った先のマップ名になる");
            Assert.AreEqual(sim.Map.Name, minimap.MapName);
            var moved = minimap.Layout!.ToMinimap(sim.Motor.X, sim.Motor.Y);
            Assert.AreEqual(moved.X, minimap.SelfDotPosition.x, 1f, "移った先の位置にすぐ合わせる");
            Assert.AreNotSame(field01Layout, minimap.Layout, "描き方を作り直す");
            Assert.IsNotNull(minimap.TerrainTexture);
            yield return null;
            GameSmokeTests.SaveScreenshot(bootstrap, "minimap-field02.png");

            Object.Destroy(bootstrap.gameObject);
            yield return null;
        }

        private static GameBootstrap CreateBootstrap(ScriptedInputSource input, int startMapId)
        {
            var go = new GameObject("GameBootstrap");
            var bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.StartMapId = startMapId;
            bootstrap.InputSource = input;
            return bootstrap;
        }
    }
}
