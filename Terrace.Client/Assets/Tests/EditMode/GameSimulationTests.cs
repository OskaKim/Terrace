using NUnit.Framework;
using Terrace.Client.Core;
using Terrace.Map;

namespace Terrace.Client.Tests.EditMode
{
    [TestFixture]
    public class GameSimulationTests
    {
        private const float Dt = 1f / 60f;

        private static void Run(GameSimulation sim, InputFrame input, float seconds)
        {
            var steps = (int)(seconds / Dt + 0.5f);
            for (var i = 0; i < steps; i++)
            {
                sim.Step(input, Dt);
                input.JumpPressed = false;
                input.AttackPressed = false;
                input.PickupPressed = false;
            }
        }

        [Test]
        public void スポーン地点はspawnポータルで敵は湧き点から湧いている()
        {
            var sim = TestMaps.NewSimulation();

            Assert.AreEqual(2f, sim.SpawnPosition.X);
            Assert.AreEqual(0f, sim.SpawnPosition.Y);
            Assert.AreEqual(MotorMode.Ground, sim.Motor.Mode);
            Assert.AreEqual(2, sim.World.Enemies.Count);
            Assert.AreEqual(100, sim.Player.Hp);
        }

        [Test]
        public void 敵に触れるとダメージを受けて吹き飛び無敵時間中は続けて受けない()
        {
            var sim = TestMaps.NewSimulation();
            var damaged = 0;
            sim.PlayerDamaged += _ => damaged++;
            sim.Motor.Teleport(24.5f, 5f); // Slime (25, 5) の隣

            sim.Step(InputFrame.None, Dt);

            Assert.AreEqual(99, sim.Player.Hp);
            Assert.AreEqual(1, damaged);
            Assert.IsTrue(sim.Player.IsInvulnerable);
            Assert.AreEqual(MotorMode.Air, sim.Motor.Mode, "ノックバックで吹き飛ぶ");
            Assert.Less(sim.Motor.VelocityX, 0f, "敵と反対側(左)へ");

            Run(sim, InputFrame.None, 0.5f);
            Assert.AreEqual(99, sim.Player.Hp, "無敵時間中は続けて受けない");
            Assert.IsTrue(sim.Messages.Contains("Slime から 1 ダメージ"));
        }

        [Test]
        public void HPが0になると倒れ時間が経つとスポーン地点で復活する()
        {
            var sim = TestMaps.NewSimulation(map: TestMaps.Sample());
            var died = 0;
            var respawned = 0;
            sim.PlayerDied += () => died++;
            sim.PlayerRespawned += () => respawned++;
            var goblin = sim.World.Enemies[1]; // 攻撃 5。足場 #5 を巡回している
            Assert.AreEqual("Goblin", goblin.Definition.Name);

            for (var i = 0; i < 60 * 30 && !sim.Player.IsDead; i++)
            {
                // 巡回して離れていく Goblin の上へ毎フレーム戻し、無敵が切れるたびに当て続ける
                sim.Motor.Teleport(goblin.X, goblin.Y);
                sim.Step(InputFrame.None, Dt);
            }

            Assert.IsTrue(sim.Player.IsDead);
            Assert.AreEqual(0, sim.Player.Hp);
            Assert.AreEqual(1, died);
            Assert.IsTrue(sim.Messages.Contains("倒れた"));

            Run(sim, InputFrame.None, 2.1f); // 復活後に歩き出さないよう入力なしで待つ

            Assert.IsFalse(sim.Player.IsDead);
            Assert.AreEqual(100, sim.Player.Hp);
            Assert.AreEqual(1, respawned);
            Assert.AreEqual(2f, sim.Motor.X, 0.001f, "spawn ポータルの位置");
            Assert.AreEqual(0f, sim.Motor.Y, 0.001f);
        }

        [Test]
        public void 落下するとワールドの外で倒れる()
        {
            var sim = TestMaps.NewSimulation();
            sim.Motor.Teleport(28f, 5f);

            Run(sim, InputFrame.Hold(right: true), 3f);

            Assert.IsTrue(sim.Player.IsDead);
            Assert.IsTrue(sim.Messages.Contains("落下"));
        }

        [Test]
        public void 敵を倒すとキル数が増えドロップを拾うとインベントリに入る()
        {
            var sim = TestMaps.NewSimulation();
            AttackOutcome? last = null;
            sim.Attacked += o => last = o;
            sim.Motor.Teleport(23.8f, 5f);
            Run(sim, InputFrame.None, 1.1f);           // テレポート直後の無敵などを消化
            sim.Motor.Teleport(23.8f, 5f);

            sim.Step(InputFrame.Hold(right: true).WithAttack(), Dt);

            Assert.IsTrue(last.HasValue && last.Value.Killed, "Slime は HP 10 なので一撃");
            Assert.AreEqual(1, sim.Player.Kills);
            Assert.IsTrue(sim.Messages.Contains("Slime を倒した"));
            Assert.IsTrue(sim.Messages.Contains("Potion"));
            Assert.AreEqual(2, sim.World.Drops.Count);

            Run(sim, InputFrame.Hold(right: true), 0.4f); // ドロップの近くまで歩く
            sim.Step(InputFrame.None.WithPickup(), Dt);

            Assert.AreEqual(1, sim.Player.Inventory.Count);
            Assert.IsTrue(sim.Messages.Contains("を拾った"));
        }

        [Test]
        public void 同じマップ内のポータルで移動する()
        {
            var sim = TestMaps.NewSimulation(map: TestMaps.FlatWithPortals());
            Portal? used = null;
            Portal? target = null;
            sim.PortalUsed += (from, to) => { used = from; target = to; };
            Run(sim, InputFrame.None, 1.1f);

            sim.Step(InputFrame.Hold(up: true), Dt);

            Assert.IsNotNull(used);
            Assert.AreEqual("spawn", used!.Name);
            Assert.AreEqual("far", target!.Name);
            Assert.AreEqual(30f, sim.Motor.X, 0.001f);
            Assert.AreEqual(MotorMode.Ground, sim.Motor.Mode);
        }

        [Test]
        public void 行き先の無いポータルではメッセージだけ出す()
        {
            var sim = TestMaps.NewSimulation(); // spawn ポータルは map 2 行き
            Run(sim, InputFrame.None, 1.1f);

            sim.Step(InputFrame.Hold(up: true), Dt);

            Assert.AreEqual(2f, sim.Motor.X, 0.001f);
            Assert.IsTrue(sim.Messages.Contains("まだありません"));
        }
    }
}
