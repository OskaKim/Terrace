using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Terrace.Client.Core;
using Terrace.Client.Online;
using Terrace.Client.Unity;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Terrace.Client.Tests.PlayMode
{
    /// <summary>
    /// ログイン窓と接続。
    /// サーバーが要らないもの(窓の操作・接続の失敗)は常に走る。
    /// 実際に 2 人で繋ぐものは環境変数 TERRACE_SERVER があるときだけ走る(tools/e2e-online.ps1 が立てて渡す)。
    /// </summary>
    public class OnlineSmokeTests
    {
        [UnityTest]
        public IEnumerator ログイン窓でひとりで遊ぶを選ぶとオフラインで始まる()
        {
            var go = new GameObject("GameBootstrap");
            var bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Startup = StartupMode.Login;
            bootstrap.InputSource = new ScriptedInputSource();
            yield return null;

            Assert.IsFalse(bootstrap.IsReady, "選ぶまでは始まらない");
            Assert.IsNotNull(bootstrap.LoginWindow);
            Assert.IsTrue(bootstrap.LoginWindow!.IsOpen);
            Assert.IsNotEmpty(bootstrap.LoginWindow.PlayerName, "名前の初期値が入っている");
            Assert.IsNotEmpty(bootstrap.LoginWindow.ServerAddress);

            bootstrap.LoginWindow.ClickOffline();
            yield return null;

            Assert.IsNull(bootstrap.LastError, bootstrap.LastError);
            Assert.IsTrue(bootstrap.IsReady);
            Assert.IsNull(bootstrap.LoginWindow, "窓は閉じる");
            Assert.IsFalse(bootstrap.Simulation!.IsOnline);
            Assert.AreEqual("オフライン", HudView.OnlineStatusText(bootstrap.Simulation));
            Assert.IsNotNull(GameObject.Find("Player"));

            Object.Destroy(go);
            yield return null;
            Assert.IsNull(GameObject.Find("Player"), "起動役を消すと、作った物も消える");
            Assert.IsNull(GameObject.Find("Map"));
            Assert.IsNull(GameObject.Find("ShopCanvas"));
        }

        [UnityTest]
        public IEnumerator サーバーに繋がらなければログイン窓に理由を出して選び直せる()
        {
            var go = new GameObject("GameBootstrap");
            var bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Startup = StartupMode.Login;
            bootstrap.InputSource = new ScriptedInputSource();
            yield return null;

            // 誰も待ち受けていないポート。YetAnotherHttpHandler(ネイティブ)と MagicOnion の読み込みもここで確かめる
            var task = bootstrap.ConnectAndStartAsync("nobody", "http://127.0.0.1:1");
            Assert.IsTrue(bootstrap.IsConnecting);
            yield return WaitUntil(() => task.IsCompleted, 15f);

            Assert.IsFalse(task.Result);
            Assert.IsFalse(bootstrap.IsConnecting);
            Assert.IsFalse(bootstrap.IsReady);
            Assert.IsNotNull(bootstrap.LastConnectError);
            Debug.Log($"[online-smoke] expected connect error: {bootstrap.LastConnectError}");
            Assert.IsTrue(bootstrap.LoginWindow!.IsOpen, "窓は開いたまま");

            bootstrap.LoginWindow.ClickOffline();
            yield return null;
            Assert.IsTrue(bootstrap.IsReady, "失敗のあとでもひとりで始められる");

            Object.Destroy(go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator サーバーがあれば二人が同じマップで互いに見え移動と攻撃が伝わる()
        {
            var server = Environment.GetEnvironmentVariable("TERRACE_SERVER");
            if (string.IsNullOrEmpty(server))
            {
                Assert.Ignore("環境変数 TERRACE_SERVER が無いので省略 (tools/e2e-online.ps1 で実行する)");
            }

            // A: 画面付きのクライアント(草原から始める)
            var go = new GameObject("GameBootstrap");
            var alice = go.AddComponent<GameBootstrap>();
            alice.Startup = StartupMode.Login;
            alice.StartMapId = 1;
            var input = new ScriptedInputSource();
            alice.InputSource = input;
            yield return null;

            var connectA = alice.ConnectAndStartAsync("alice-e2e", server!);
            yield return WaitUntil(() => connectA.IsCompleted, 15f);
            Assert.IsTrue(connectA.Result, alice.LastConnectError);
            var simA = alice.Simulation!;
            yield return WaitUntil(() => simA.Online?.IsSynchronized == true, 10f);
            Assert.IsTrue(simA.Online!.IsSynchronized, "A がスナップショットを受け取る");
            Assert.IsNotEmpty(simA.World.Enemies, "サーバーの敵が映る");

            // B: 画面なし(Core だけ)。毎フレーム自分で進める
            var connectB = MagicOnionConnection.ConnectAsync(server!, "bob-e2e");
            yield return WaitUntil(() => connectB.IsCompleted, 15f);
            Assert.IsTrue(connectB.Status == System.Threading.Tasks.TaskStatus.RanToCompletion, connectB.Exception?.ToString());
            var bob = connectB.Result;
            var master = alice.MasterData;
            var simB = new GameSimulation(
                alice.Maps!.Get(1)!,
                id => master?.GetEnemy(id) ?? FallbackEnemies.Get(id),
                id => $"item{id}",
                online: bob,
                inbox: bob.Inbox);
            var inputB = InputFrame.None;
            var stepB = new StepAlong(simB, () => inputB);

            try
            {
                yield return stepB.Until(() => simB.Online?.IsSynchronized == true && simA.RemotePlayers.Find(bob.Self.PlayerId) != null, 10f);
                Assert.IsTrue(simB.Online!.IsSynchronized, "B がスナップショットを受け取る");
                var bobSeenByA = simA.RemotePlayers.Find(bob.Self.PlayerId);
                Assert.IsNotNull(bobSeenByA, "A に B が見える");
                Assert.AreEqual("bob-e2e", bobSeenByA!.Name);
                Assert.IsNotNull(simB.RemotePlayers.Find(simA.Online!.Self.PlayerId), "B に A が見える");
                Assert.AreEqual(1, alice.RemotePlayerViewCount, "A の画面に B の見た目が出る");
                Assert.AreEqual(
                    simA.World.Enemies.Select(e => e.InstanceId).OrderBy(i => i).ToArray(),
                    simB.World.Enemies.Select(e => e.InstanceId).OrderBy(i => i).ToArray(),
                    "二人に同じ敵が見える");

                // B が右へ歩くと A の画面でも動く
                var bobStartX = bobSeenByA.X;
                inputB = InputFrame.Hold(right: true);
                yield return stepB.For(1.0f);
                inputB = InputFrame.None;
                yield return stepB.For(0.5f);
                Assert.Greater(bobSeenByA.X, bobStartX + 2f, "B の移動が A に届く");
                Assert.AreEqual(simB.Motor.X, bobSeenByA.X, 0.5f, "止まれば同じ位置に落ち着く");

                // A が敵を攻撃すると B の世界でも HP が減る(または倒れる)
                var target = simA.World.Enemies.First(e => e.IsAlive);
                var targetInB = simB.World.FindEnemy(target.InstanceId)!;
                var hit = false;
                for (var attempt = 0; attempt < 20 && !hit; attempt++)
                {
                    simA.Motor.Teleport(target.X - 1.0f, target.Y);
                    input.Current = InputFrame.Hold(right: true);
                    yield return stepB.Frames(1);
                    input.Current = InputFrame.None.WithAttack();
                    yield return stepB.For(0.45f);
                    hit = targetInB.IsDead || targetInB.Hp < targetInB.MaxHp;
                }
                Assert.IsTrue(hit, "A の攻撃が B の世界に届く");
                Assert.IsTrue(simA.Messages.Contains("ダメージ") || simA.Messages.Contains("倒した"), "A に結果の通知が届く");

                SaveScreenshot(alice, "online.png");

                // B が抜けると A から消える
                bob.Dispose();
                yield return stepB.Until(() => simA.RemotePlayers.Find(bob.Self.PlayerId) == null, 5f, stepSim: false);
                Assert.IsNull(simA.RemotePlayers.Find(bob.Self.PlayerId), "B の退出が A に届く");
                Assert.IsTrue(simA.Messages.Contains("bob-e2e が去った"));
            }
            finally
            {
                bob.Dispose();
                Object.Destroy(go);
            }
            yield return null;
        }

        private static IEnumerator WaitUntil(Func<bool> condition, float timeoutSeconds)
        {
            var deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>画面の無いもう 1 人を、Unity のフレームに合わせて進める。</summary>
        private sealed class StepAlong
        {
            private readonly GameSimulation _sim;
            private readonly Func<InputFrame> _input;

            public StepAlong(GameSimulation sim, Func<InputFrame> input)
            {
                _sim = sim;
                _input = input;
            }

            public IEnumerator Frames(int count)
            {
                for (var i = 0; i < count; i++)
                {
                    yield return null;
                    _sim.Step(_input(), Mathf.Min(Time.deltaTime, 0.05f));
                }
            }

            public IEnumerator For(float seconds)
            {
                var end = Time.realtimeSinceStartup + seconds;
                while (Time.realtimeSinceStartup < end)
                {
                    yield return null;
                    _sim.Step(_input(), Mathf.Min(Time.deltaTime, 0.05f));
                }
            }

            public IEnumerator Until(Func<bool> condition, float timeoutSeconds, bool stepSim = true)
            {
                var deadline = Time.realtimeSinceStartup + timeoutSeconds;
                while (!condition() && Time.realtimeSinceStartup < deadline)
                {
                    yield return null;
                    if (stepSim) _sim.Step(_input(), Mathf.Min(Time.deltaTime, 0.05f));
                }
            }
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
                File.WriteAllBytes(Path.Combine(directory, fileName), texture.EncodeToPNG());
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
