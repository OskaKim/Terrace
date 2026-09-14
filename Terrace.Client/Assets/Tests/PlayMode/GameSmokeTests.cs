using System.Collections;
using System.IO;
using NUnit.Framework;
using Terrace.Client.Core;
using Terrace.Client.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace Terrace.Client.Tests.PlayMode
{
    /// <summary>実際に GameBootstrap を起動し、歩く・敵を倒す・拾うまでを通す。最後に画面を PNG に書き出す。</summary>
    public class GameSmokeTests
    {
        [UnityTest]
        public IEnumerator 起動して歩き敵を倒して拾える()
        {
            var go = new GameObject("GameBootstrap");
            var bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.MapFileName = "field01.json";
            var input = new ScriptedInputSource();
            bootstrap.InputSource = input;

            yield return null; // Start()
            Assert.IsNull(bootstrap.LastError, bootstrap.LastError);
            Assert.IsTrue(bootstrap.IsReady);
            var sim = bootstrap.Simulation!;
            Assert.IsNotNull(bootstrap.MasterData, "master.bytes が読めていること");
            Assert.IsNotNull(bootstrap.Art, "Kenney の素材が読めていること");
            Assert.AreEqual(5, sim.World.Enemies.Count);
            Assert.AreEqual(1.31f, sim.PlayerConfig.Height, 0.05f, "当たり判定の高さが絵に合っている");

            // 右へ歩く
            var startX = sim.Motor.X;
            input.Current = InputFrame.Hold(right: true);
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(sim.Motor.X, startX + 1f, "右へ歩いている");
            Assert.AreEqual(MotorMode.Ground, sim.Motor.Mode);

            // 最初の Slime の左隣へ移動して攻撃(Slime は HP 10 なので一撃)
            input.Current = InputFrame.None;
            var slime = sim.World.Enemies[0];
            Assert.AreEqual("Slime", slime.Definition.Name);
            sim.Motor.Teleport(slime.X - 1.0f, slime.Y);
            input.Current = InputFrame.Hold(right: true);
            yield return null;
            input.Current = InputFrame.None.WithAttack();
            yield return null;
            yield return null;
            Assert.IsTrue(slime.IsDead, "攻撃で Slime が死ぬ");
            Assert.AreEqual(1, sim.Player.Kills);

            // ドロップを拾う
            if (sim.World.Drops.Count > 0)
            {
                var drop = sim.World.Drops[0];
                sim.Motor.Teleport(drop.X, drop.Y);
                input.Current = InputFrame.None.WithPickup();
                yield return null;
                yield return null;
                Assert.AreEqual(1, sim.Player.Inventory.Count, "拾ったアイテムが持ち物に入る");
            }

            input.Current = InputFrame.None;
            yield return null;
            SaveScreenshot(bootstrap, "smoke.png");

            Object.Destroy(go);
            yield return null;
        }

        private static void SaveScreenshot(GameBootstrap bootstrap, string fileName)
        {
            var camera = bootstrap.Camera;
            if (camera == null) return;

            const int width = 1280;
            const int height = 720;
            var renderTexture = new RenderTexture(width, height, 24);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            try
            {
                camera.targetTexture = renderTexture;
                camera.Render();
                RenderTexture.active = renderTexture;
                var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
                texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                texture.Apply();
                var directory = Path.Combine(Application.dataPath, "..", "Logs");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, fileName);
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Debug.Log($"[smoke] screenshot: {path}");
                Object.Destroy(texture);
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                Object.Destroy(renderTexture);
            }
        }
    }
}
