using System.Collections;
using NUnit.Framework;
using Terrace.Client.Core;
using Terrace.Client.Presentation;
using Terrace.Client.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace Terrace.Client.Tests.PlayMode
{
    /// <summary>
    /// 背景の層(BackdropView)。草原はマップの backgrounds、町はテーマの既定の組で描く。
    /// カメラが動いたフレームのうちに、そのカメラ位置で層を置く(1 フレーム遅れない)。
    /// 草原の左端・中央・右端と町の画面を書き出す(Logs/backdrop-*.png)。
    /// </summary>
    public class BackdropTests
    {
        [UnityTest]
        public IEnumerator 草原の背景はカメラが動いたフレームのカメラ位置で置かれる()
        {
            var input = new ScriptedInputSource();
            var bootstrap = CreateBootstrap(input, startMapId: 1);
            yield return null; // Start()
            Assert.IsNull(bootstrap.LastError, bootstrap.LastError);
            var sim = bootstrap.Simulation!;
            var camera = bootstrap.Camera!;
            Assert.AreEqual(1, sim.Map.Id);

            var backdrop = Object.FindAnyObjectByType<BackdropView>();
            Assert.IsNotNull(backdrop, "背景がある");
            Assert.AreEqual(sim.Map.Backgrounds.Count, backdrop!.LayerCount, "草原の backgrounds の層を全部描く(絵が全部ある)");

            // 右へ歩き続け、カメラが追いかけている間の各フレームで確かめる。
            // yield return null で戻るのは次のフレームの Update の後・LateUpdate の前なので、
            // カメラも背景も前のフレームの LateUpdate で決まった位置のまま。背景がカメラより先に動いていれば、2 フレーム前のカメラ位置とずれる
            input.Current = InputFrame.Hold(right: true);
            var previous = camera.transform.position;
            var moved = 0;
            for (var frame = 0; frame < 40; frame++)
            {
                yield return null;
                var position = camera.transform.position;
                if (Mathf.Abs(position.x - previous.x) > 1e-4f) moved++;
                previous = position;
                AssertPlacedAt(backdrop, position);
            }
            input.Current = InputFrame.None;
            Assert.Greater(moved, 10, "カメラが動いているフレームで確かめた");

            // 左端・中央・右端の画面
            var rig = camera.GetComponent<CameraRig>();
            foreach (var (x, y, name) in new[] { (3f, 0f, "left"), (40f, 4f, "center"), (78f, 0f, "right") })
            {
                sim.Motor.Teleport(x, y);
                rig.SnapToTarget();
                yield return null;
                AssertPlacedAt(backdrop, camera.transform.position);
                GameSmokeTests.SaveScreenshot(bootstrap, $"backdrop-field-{name}.png");
            }

            Object.Destroy(bootstrap.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 背景の無い町はテーマの既定の組で描きマップを移ると作り直す()
        {
            var bootstrap = CreateBootstrap(new ScriptedInputSource(), startMapId: 100);
            yield return null; // Start()
            Assert.IsNull(bootstrap.LastError, bootstrap.LastError);
            var sim = bootstrap.Simulation!;
            Assert.IsEmpty(sim.Map.Backgrounds, "町には backgrounds を書いていない");

            var town = Object.FindAnyObjectByType<BackdropView>();
            Assert.IsNotNull(town, "背景がある");
            var theme = bootstrap.Art!.Theme(sim.Map.Theme);
            Assert.AreEqual(theme.BackgroundPath, town!.LayerAt(0).Sprite, "テーマの背景");
            Assert.AreEqual(3, town.LayerCount, "テーマの背景 1 枚と雲 2 層");
            AssertPlacedAt(town, bootstrap.Camera!.transform.position);
            GameSmokeTests.SaveScreenshot(bootstrap, "backdrop-town.png");

            sim.Travel.ChangeMap(bootstrap.Maps!.Get(1)!);
            yield return null; // 前のマップの背景は、このフレームの終わりで消える
            yield return null;
            var field = Object.FindAnyObjectByType<BackdropView>();
            Assert.IsNotNull(field);
            Assert.AreEqual(sim.Map.Backgrounds.Count, field!.LayerCount, "草原の backgrounds で作り直す");
            Assert.AreEqual(1, Object.FindObjectsByType<BackdropView>(FindObjectsSortMode.None).Length, "前のマップの背景は残らない");

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

        /// <summary>各層が、このカメラ位置から計算した位置にある。</summary>
        private static void AssertPlacedAt(BackdropView backdrop, Vector3 cameraPosition)
        {
            var center = backdrop.MapCenter;
            for (var i = 0; i < backdrop.LayerCount; i++)
            {
                var layer = backdrop.LayerAt(i);
                var expectedX = BackdropLayout.AnchorX(layer, center.x, cameraPosition.x, backdrop.ScrollTime);
                var expectedY = BackdropLayout.AnchorY(layer, center.y, cameraPosition.y);
                var anchor = backdrop.AnchorOf(i);
                Assert.AreEqual(expectedX, anchor.x, 1e-3f, $"{layer.Sprite} の X(カメラ {cameraPosition.x:F3})");
                Assert.AreEqual(expectedY, anchor.y, 1e-3f, $"{layer.Sprite} の Y(カメラ {cameraPosition.y:F3})");

                var drawn = 0;
                foreach (var renderer in backdrop.RenderersOf(i))
                {
                    if (!renderer.enabled) continue;
                    drawn++;
                    var position = renderer.transform.position;
                    Assert.AreEqual(anchor.y, position.y, 1e-3f, $"{layer.Sprite} の絵の高さ");
                    if (!layer.TileX) Assert.AreEqual(anchor.x, position.x, 1e-3f, $"{layer.Sprite} の絵の X");
                }
                if (layer.TileX) Assert.Greater(drawn, 0, $"{layer.Sprite} は画面に掛かる絵がある");
            }
        }
    }
}
