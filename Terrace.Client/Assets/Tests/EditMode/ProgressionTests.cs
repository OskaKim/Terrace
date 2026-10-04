using System;
using System.Collections.Generic;
using NUnit.Framework;
using Terrace.Client.Core;
using Terrace.Map;

namespace Terrace.Client.Tests.EditMode
{
    /// <summary>経験値とレベルの規則(PlayerProgression)と、それをオフラインの GameSimulation に繋いだ所を確かめる。オンラインは OnlineSimulationTests。</summary>
    [TestFixture]
    public class ProgressionTests
    {
        private const float Dt = 1f / 60f;

        // ---- PlayerProgression ----

        [Test]
        public void 始めはレベル1で経験値0で能力は1行目の値()
        {
            var progression = new PlayerProgression(TestMaps.Levels());

            Assert.AreEqual((1, 0, 5), (progression.Level, progression.Exp, progression.ExpToNext));
            Assert.AreEqual((100, 10), (progression.MaxHp, progression.Attack));
            Assert.IsFalse(progression.IsMaxLevel);
            Assert.AreEqual(0f, progression.ExpRatio);
        }

        [Test]
        public void ExpToNextに届くとレベルが上がり余りが持ち越される()
        {
            var progression = new PlayerProgression(TestMaps.Levels());

            Assert.AreEqual(0, progression.AddExp(4));
            Assert.AreEqual((1, 4), (progression.Level, progression.Exp));
            Assert.AreEqual(0.8f, progression.ExpRatio, 1e-5f);

            Assert.AreEqual(1, progression.AddExp(3));
            Assert.AreEqual((2, 2), (progression.Level, progression.Exp), "7 - 5 = 2 を持ち越す");
            Assert.AreEqual((120, 14, 6), (progression.MaxHp, progression.Attack, progression.ExpToNext));
        }

        [Test]
        public void 一度に2つ以上上がることもある()
        {
            var progression = new PlayerProgression(TestMaps.Levels());

            Assert.AreEqual(2, progression.AddExp(5 + 6 + 3));

            Assert.AreEqual((3, 3), (progression.Level, progression.Exp));
            Assert.AreEqual((150, 20), (progression.MaxHp, progression.Attack));
        }

        [Test]
        public void 最高レベルでは経験値が増えない()
        {
            var progression = new PlayerProgression(TestMaps.Levels());

            Assert.AreEqual(3, progression.AddExp(5 + 6 + 10 + 7));
            Assert.AreEqual((4, 0), (progression.Level, progression.Exp), "最高レベルに届いた余りは捨てる");
            Assert.IsTrue(progression.IsMaxLevel);
            Assert.AreEqual(0, progression.ExpToNext);
            Assert.AreEqual(1f, progression.ExpRatio);

            Assert.AreEqual(0, progression.AddExp(100));
            Assert.AreEqual((4, 0), (progression.Level, progression.Exp));
        }

        [Test]
        public void 経験値が0以下なら何も変わらない()
        {
            var progression = new PlayerProgression(TestMaps.Levels());

            Assert.AreEqual(0, progression.AddExp(0));
            Assert.AreEqual(0, progression.AddExp(-3));
            Assert.AreEqual((1, 0), (progression.Level, progression.Exp));
        }

        [Test]
        public void 表の最後の行はExpToNextが0でなくても最高レベルとして扱う()
        {
            var table = new LevelTable(new[]
            {
                new LevelDefinition { Level = 2, ExpToNext = 4, MaxHp = 20, Attack = 2 },
                new LevelDefinition { Level = 1, ExpToNext = 4, MaxHp = 10, Attack = 1 },
            });
            var progression = new PlayerProgression(table);

            Assert.AreEqual(1, progression.Level, "行の順に関係なく一番小さいレベルから始める");
            Assert.AreEqual(1, progression.AddExp(100));
            Assert.AreEqual((2, 0), (progression.Level, progression.Exp));
            Assert.IsTrue(progression.IsMaxLevel);
        }

        [Test]
        public void 逃げ道のレベル表は1から欠けなく続き最後の行だけが最高レベル()
        {
            var table = LevelTable.Fallback;

            Assert.AreEqual(1, table.MinLevel);
            Assert.Greater(table.MaxLevel, 1, "上がれる");
            for (var level = table.MinLevel; level <= table.MaxLevel; level++)
            {
                var row = table.Find(level);
                Assert.IsNotNull(row, $"Level {level}");
                Assert.Greater(row!.MaxHp, 0);
                Assert.Greater(row.Attack, 0);
                if (level < table.MaxLevel) Assert.Greater(row.ExpToNext, 0, $"Level {level}");
                else Assert.AreEqual(0, row.ExpToNext, "最高レベル");
            }
        }

        // ---- GameSimulation(オフライン) ----

        [Test]
        public void オフラインで敵を倒すとその敵のExpだけ経験値が増える()
        {
            var sim = NewSimulation(TestMaps.Field());

            KillFirstEnemy(sim);

            Assert.AreEqual(1, sim.Player.Kills);
            Assert.AreEqual((1, 3), (sim.Player.Level, sim.Player.Exp), "Slime の Exp");
            Assert.IsTrue(sim.Messages.Contains("Slime を倒した (+10 メソ, +3 EXP)"));
        }

        [Test]
        public void レベルが上がると最大HPと攻撃力が新しい行の値になりHPが全快しLeveledUpが上がった数だけ起きる()
        {
            var sim = NewSimulation(OneEnemyMap(), id => new EnemyDefinition { EnemyId = id, Name = "Boss", MaxHp = 10, Exp = 5 + 6 + 1, DropRate = 0f });
            var leveled = new List<int>();
            sim.Rewards.LeveledUp += level => leveled.Add(level);
            sim.Player.Hp = 40;

            KillFirstEnemy(sim);

            Assert.AreEqual((3, 1), (sim.Player.Level, sim.Player.Exp));
            Assert.AreEqual((150, 20), (sim.Player.MaxHp, sim.Player.Attack));
            Assert.AreEqual(150, sim.Player.Hp, "新しい最大 HP まで全快する");
            Assert.AreEqual(new[] { 2, 3 }, leveled);
            Assert.IsTrue(sim.Messages.Contains("レベルが上がった! Lv. 3"));
        }

        [Test]
        public void 最高レベルで敵を倒しても経験値は増えない()
        {
            var sim = NewSimulation(TestMaps.Field());
            sim.Player.GainExp(5 + 6 + 10);
            Assert.IsTrue(sim.Player.Progression.IsMaxLevel);
            var leveled = 0;
            sim.Rewards.LeveledUp += _ => leveled++;

            KillFirstEnemy(sim);

            Assert.AreEqual((4, 0), (sim.Player.Level, sim.Player.Exp));
            Assert.AreEqual(0, leveled);
            Assert.IsTrue(sim.Messages.Contains("Slime を倒した (+10 メソ)"), "得ていない経験値は流さない");
        }

        [Test]
        public void 死んで復活しても経験値とレベルは変わらない()
        {
            var sim = NewSimulation(TestMaps.Sample());
            sim.Player.GainExp(5 + 2);
            Assert.AreEqual((2, 2), (sim.Player.Level, sim.Player.Exp));
            sim.Motor.Teleport(28f, 5f);

            Run(sim, InputFrame.Hold(right: true), 3f); // 崖から落ちる
            Assert.IsTrue(sim.Player.IsDead);
            Assert.AreEqual((2, 2), (sim.Player.Level, sim.Player.Exp), "倒れても減らない");

            Run(sim, InputFrame.None, 2.1f);
            Assert.IsFalse(sim.Player.IsDead);
            Assert.AreEqual((2, 2), (sim.Player.Level, sim.Player.Exp), "復活しても減らない");
            Assert.AreEqual(120, sim.Player.Hp, "レベル 2 の最大 HP で復活する");
        }

        [Test]
        public void 倒れている間にレベルが上がってもHPは全快しない()
        {
            var sim = NewSimulation(TestMaps.Sample());
            sim.Motor.Teleport(28f, 5f);
            Run(sim, InputFrame.Hold(right: true), 3f);
            Assert.IsTrue(sim.Player.IsDead);

            Assert.AreEqual(1, sim.Player.GainExp(5));
            Assert.AreEqual(0, sim.Player.Hp);
        }

        [Test]
        public void レベル2で攻撃すると敵に与えるダメージがレベル2のAttackになる()
        {
            var sim = NewSimulation(TestMaps.Sample());
            sim.Player.GainExp(5);
            Assert.AreEqual((2, 14), (sim.Player.Level, sim.Player.Attack));
            var goblin = sim.World.Enemies[1];
            Assert.AreEqual("Goblin", goblin.Definition.Name);
            AttackOutcome? last = null;
            sim.Combat.Attacked += outcome => last = outcome;

            AttackOnce(sim, goblin);

            Assert.IsTrue(last.HasValue && last.Value.Hit);
            Assert.AreEqual(14, last!.Value.Damage);
            Assert.AreEqual(30 - 14, goblin.Hp);
        }

        // ---- 道具 ----

        private static GameSimulation NewSimulation(MapData map, Func<int, EnemyDefinition?>? lookup = null)
            => new GameSimulation(map, lookup ?? TestMaps.Lookup, TestMaps.ItemName, random: new Random(1), levels: TestMaps.Levels());

        /// <summary>平らな足場に湧き点が 1 つだけのマップ。</summary>
        private static MapData OneEnemyMap()
        {
            return new MapData
            {
                Id = 50,
                Name = "Arena",
                Bounds = new WorldBounds { Left = -5, Right = 45, Top = 20, Bottom = -8 },
                Footholds = new List<Foothold> { new Foothold { Id = 1, X1 = 0, Y1 = 0, X2 = 40, Y2 = 0 } },
                Portals = new List<Portal> { new Portal { Id = 1, Name = "spawn", X = 5, Y = 0, Kind = PortalKind.Spawn } },
                SpawnPoints = new List<SpawnPoint> { new SpawnPoint { Id = 1, X = 20, Y = 0, EnemyId = 9, RespawnSeconds = 100 } },
            };
        }

        /// <summary>巡回している敵の左隣へ移り、右を向いて 1 回攻撃する。攻撃硬直が解けるまで待ってから戻る。</summary>
        private static void AttackOnce(GameSimulation sim, EnemyEntity enemy)
        {
            sim.Motor.Teleport(enemy.X - 1.2f, enemy.Y);
            sim.Step(InputFrame.Hold(right: true).WithAttack(), Dt);
            Run(sim, InputFrame.None, 0.6f);
        }

        private static void KillFirstEnemy(GameSimulation sim)
        {
            var enemy = sim.World.Enemies[0];
            for (var i = 0; i < 20 && !enemy.IsDead; i++) AttackOnce(sim, enemy);
            Assert.IsTrue(enemy.IsDead, $"{enemy.Definition.Name} を倒せなかった");
        }

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
    }
}
