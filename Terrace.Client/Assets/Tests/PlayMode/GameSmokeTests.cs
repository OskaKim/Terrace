using System.Collections;
using System.IO;
using NUnit.Framework;
using Terrace.Client.Core;
using Terrace.Client.Unity;
using UnityEngine;
using UnityEngine.TestTools;

namespace Terrace.Client.Tests.PlayMode
{
    /// <summary>
    /// 実際に GameBootstrap を起動し、町で NPC をクリックして店で買い、門から狩場へ移り、歩いて敵を倒して拾うまでを通す。
    /// 途中で画面を PNG に書き出す(Logs/smoke-shop.png, Logs/smoke.png)。
    /// </summary>
    public class GameSmokeTests
    {
        [UnityTest]
        public IEnumerator 町で店を開いて買い狩場へ移って敵を倒して拾える()
        {
            var go = new GameObject("GameBootstrap");
            var bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.StartMapId = 100;
            var input = new ScriptedInputSource();
            bootstrap.InputSource = input;

            yield return null; // Start()
            Assert.IsNull(bootstrap.LastError, bootstrap.LastError);
            Assert.IsTrue(bootstrap.IsReady);
            var sim = bootstrap.Simulation!;
            Assert.IsNotNull(bootstrap.MasterData, "master.bytes が読めていること");
            Assert.IsNotNull(bootstrap.Art, "Kenney の素材が読めていること");
            Assert.AreEqual(100, sim.Map.Id, "町から始まる");
            Assert.AreEqual(3, bootstrap.NpcViews.Count);
            Assert.IsEmpty(sim.World.Enemies);
            Assert.AreEqual(1.31f, sim.PlayerConfig.Height, 0.05f, "当たり判定の高さが絵に合っている");

            // NPC をクリックして店を開く
            var merry = sim.Map.FindNpc(1)!;
            Assert.IsFalse(bootstrap.TryClickWorld(new Vector2(merry.X + 5f, merry.Y)), "離れた場所は何も無い");
            Assert.IsTrue(bootstrap.TryClickWorld(new Vector2(merry.X, merry.Y + 0.7f)), "NPC の上をクリックすると話しかける");
            Assert.IsNotNull(sim.ActiveShop);
            Assert.IsTrue(bootstrap.ShopWindow!.IsOpen);
            yield return null;
            Assert.AreEqual(5, bootstrap.ShopWindow.GoodsRowCount, "雑貨屋は 5 品");

            var mesoBefore = sim.Player.Meso;
            Assert.AreEqual(ShopResult.Ok, sim.Buy(1, 2));
            Assert.AreEqual(mesoBefore - 100, sim.Player.Meso);
            Assert.AreEqual(2, sim.Player.CountOf(1));
            bootstrap.ShopWindow.SelectInventoryTab(ItemKind.Use);
            yield return null;
            Assert.AreEqual(1, bootstrap.ShopWindow.InventoryRowCount, "消費タブに Potion が 1 行");
            SaveScreenshot(bootstrap, "smoke-shop.png");

            // 店を開いている間は歩けない
            var xBefore = sim.Motor.X;
            input.Current = InputFrame.Hold(right: true);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(xBefore, sim.Motor.X, 0.001f);
            input.Current = InputFrame.None;

            sim.CloseShop();
            yield return null;
            Assert.IsFalse(bootstrap.ShopWindow.IsOpen);

            // 西の門から草原へ
            var gate = sim.Map.FindPortalByName("west_gate")!;
            sim.Motor.Teleport(gate.X, gate.Y);
            yield return new WaitForSeconds(1.1f); // テレポート直後のポータル待ち時間
            input.Current = InputFrame.Hold(up: true);
            yield return null;
            yield return null;
            input.Current = InputFrame.None;
            Assert.AreEqual(1, sim.Map.Id, "草原へ移動した");
            Assert.AreEqual(5, sim.World.Enemies.Count);
            Assert.AreEqual(0, bootstrap.NpcViews.Count);
            yield return null;

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
            var mesoBeforeKill = sim.Player.Meso;
            sim.Motor.Teleport(slime.X - 1.0f, slime.Y);
            input.Current = InputFrame.Hold(right: true);
            yield return null;
            input.Current = InputFrame.None.WithAttack();
            yield return null;
            yield return null;
            Assert.IsTrue(slime.IsDead, "攻撃で Slime が死ぬ");
            Assert.AreEqual(1, sim.Player.Kills);
            Assert.Greater(sim.Player.Meso, mesoBeforeKill, "倒すとメソが増える");

            // ドロップを拾う
            if (sim.World.Drops.Count > 0)
            {
                var drop = sim.World.Drops[0];
                var countBefore = sim.Player.Inventory.Count;
                sim.Motor.Teleport(drop.X, drop.Y);
                input.Current = InputFrame.None.WithPickup();
                yield return null;
                yield return null;
                Assert.AreEqual(countBefore + 1, sim.Player.Inventory.Count, "拾ったアイテムが持ち物に入る");
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
