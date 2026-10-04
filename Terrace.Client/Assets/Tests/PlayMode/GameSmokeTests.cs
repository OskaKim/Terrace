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
    /// 途中で画面を PNG に書き出す(Logs/smoke-shop.png, Logs/smoke.png、HUD 入りの Logs/smoke-hud.png)。音が読めない状態でも同じ流れが通ることも確かめる。
    /// </summary>
    public class GameSmokeTests
    {
        [UnityTest]
        public IEnumerator 町で店を開いて買い狩場へ移って敵を倒して拾える()
        {
            var bootstrap = CreateBootstrap(new ScriptedInputSource());
            yield return RunSmoke(bootstrap, saveScreenshots: true);
            Assert.AreEqual(AudioLibrary.AllSoundEffects.Count, bootstrap.AudioDirector!.AvailableCount, "効果音が全部読めている");
            Assert.AreEqual(bootstrap.Simulation!.Map.Bgm, bootstrap.AudioDirector.CurrentBgm, "狩場の曲に替わっている");
            Assert.IsNotNull(bootstrap.AudioDirector.CurrentBgmClip, "狩場の曲が読めている");

            Object.Destroy(bootstrap.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator 音が読めなくても町で店を開いて買い狩場へ移って敵を倒して拾える()
        {
            var bootstrap = CreateBootstrap(new ScriptedInputSource(), AudioLibrary.Load("Terrace/Audio/__missing__/"));
            yield return RunSmoke(bootstrap, saveScreenshots: false);
            // 例外やエラーのログが出れば、Unity の試験の枠組みがこの試験を落とす
            Assert.AreEqual(0, bootstrap.AudioDirector!.AvailableCount, "音は 1 つも読めていない");
            Assert.IsNull(bootstrap.AudioDirector.CurrentBgmClip, "BGM も読めていない");

            Object.Destroy(bootstrap.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Mキーで消音が切り替わり起動し直しても保たれる()
        {
            var hadKey = PlayerPrefs.HasKey(AudioDirector.MutedKey);
            var previous = PlayerPrefs.GetInt(AudioDirector.MutedKey, 0);
            PlayerPrefs.DeleteKey(AudioDirector.MutedKey);

            var input = new ScriptedInputSource();
            var first = CreateBootstrap(input);
            yield return null; // Start()
            Assert.IsTrue(first.IsReady);
            Assert.IsFalse(first.AudioDirector!.IsMuted, "始めは鳴る");

            input.MuteTogglePressed = true;
            yield return null;
            Assert.IsTrue(first.AudioDirector.IsMuted, "M で消音");
            Object.Destroy(first.gameObject);
            yield return null;

            var againInput = new ScriptedInputSource();
            var again = CreateBootstrap(againInput);
            yield return null;
            Assert.IsTrue(again.AudioDirector!.IsMuted, "起動し直しても消音のまま");

            againInput.MuteTogglePressed = true;
            yield return null;
            Assert.IsFalse(again.AudioDirector.IsMuted, "もう一度 M で戻る");
            Object.Destroy(again.gameObject);
            yield return null;

            if (hadKey) PlayerPrefs.SetInt(AudioDirector.MutedKey, previous);
            else PlayerPrefs.DeleteKey(AudioDirector.MutedKey);
        }

        private static GameBootstrap CreateBootstrap(ScriptedInputSource input, AudioLibrary? audio = null)
        {
            var go = new GameObject("GameBootstrap");
            var bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.StartMapId = 100;
            bootstrap.InputSource = input;
            bootstrap.Audio = audio;
            return bootstrap;
        }

        private static IEnumerator RunSmoke(GameBootstrap bootstrap, bool saveScreenshots)
        {
            var input = (ScriptedInputSource)bootstrap.InputSource;

            yield return null; // Start()
            Assert.IsNull(bootstrap.LastError, bootstrap.LastError);
            Assert.IsTrue(bootstrap.IsReady);
            var sim = bootstrap.Simulation!;
            var audio = bootstrap.AudioDirector!;
            Assert.IsNotNull(bootstrap.Camera!.GetComponent<AudioListener>(), "カメラに音を聴く耳がある");
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
            Assert.IsNotNull(sim.Trading.ActiveShop);
            Assert.IsTrue(bootstrap.ShopView!.IsOpen);
            yield return null;
            Assert.AreEqual(5, bootstrap.ShopView.GoodsRowCount, "雑貨屋は 5 品");

            var mesoBefore = sim.Player.Meso;
            Assert.AreEqual(ShopResult.Ok, sim.Trading.Buy(1, 2));
            Assert.AreEqual(mesoBefore - 100, sim.Player.Meso);
            Assert.AreEqual(2, sim.Player.CountOf(1));
            Assert.AreEqual(SoundEffect.TradeSucceeded, audio.LastRequested, "買えた音");
            bootstrap.ShopPresenter!.SelectInventoryTab(ItemKind.Use);
            yield return null;
            Assert.AreEqual(1, bootstrap.ShopView.InventoryRowCount, "消費タブに Potion が 1 行");
            if (saveScreenshots) SaveScreenshot(bootstrap, "smoke-shop.png");

            // 店を開いている間は歩けない
            var xBefore = sim.Motor.X;
            input.Current = InputFrame.Hold(right: true);
            yield return new WaitForSeconds(0.3f);
            Assert.AreEqual(xBefore, sim.Motor.X, 0.001f);
            input.Current = InputFrame.None;

            sim.Trading.CloseShop();
            yield return null;
            Assert.IsFalse(bootstrap.ShopView.IsOpen);

            // 西の門から草原へ
            var gate = sim.Map.FindPortalByName("west_gate")!;
            sim.Motor.Teleport(gate.X, gate.Y);
            yield return new WaitForSeconds(1.1f); // テレポート直後のポータル待ち時間
            input.Current = InputFrame.Hold(up: true);
            yield return null;
            yield return null;
            input.Current = InputFrame.None;
            Assert.AreEqual(1, sim.Map.Id, "草原へ移動した");
            Assert.AreEqual(SoundEffect.Portal, audio.LastRequested, "ポータルの音");
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
            Assert.AreEqual(SoundEffect.Kill, audio.LastRequested, "倒した音");

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
                Assert.AreEqual(SoundEffect.Pickup, audio.LastRequested, "拾った音");
            }

            input.Current = InputFrame.None;
            yield return null;
            if (saveScreenshots)
            {
                SaveScreenshot(bootstrap, "smoke.png");
                yield return SaveScreenWithHud("smoke-hud.png");
            }
        }

        /// <summary>
        /// HUD(IMGUI)も入った画面を書き出す。SaveScreenshot はカメラだけを描くので HUD が入らない。
        /// バッチの Unity(tools/unity.ps1)では OnGUI が呼ばれず書き出せないので、窓を開いた Unity で走らせたときだけ出る。書き出せなくても試験は落とさない。
        /// </summary>
        private static IEnumerator SaveScreenWithHud(string fileName)
        {
            var directory = Path.Combine(Application.dataPath, "..", "Logs");
            Directory.CreateDirectory(directory);
            var path = Path.GetFullPath(Path.Combine(directory, fileName));
            // 前に撮ったものは消さない(バッチで回したときに、窓を開いて撮ったものを残すため)。書き換わったかは時刻で見る
            var before = File.Exists(path) ? File.GetLastWriteTimeUtc(path) : System.DateTime.MinValue;
            ScreenCapture.CaptureScreenshot(path);
            var written = false;
            for (var i = 0; i < 30 && !written; i++)
            {
                yield return null;
                written = File.Exists(path) && File.GetLastWriteTimeUtc(path) > before;
            }
            Debug.Log(written ? $"[smoke] screenshot with HUD: {path}" : $"[smoke] HUD 入りの画面は書き出せなかった(バッチの Unity では OnGUI が呼ばれない): {path}");
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
