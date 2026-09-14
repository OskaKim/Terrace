using System;
using NUnit.Framework;
using Terrace.Client.Core;
using Terrace.Map;

namespace Terrace.Client.Tests.EditMode
{
    [TestFixture]
    public class LocalWorldTests
    {
        private static LocalWorld NewWorld(MapData? map = null)
        {
            var world = new LocalWorld(map ?? TestMaps.Sample(), TestMaps.Lookup, new Random(1));
            world.SpawnFromMap();
            return world;
        }

        [Test]
        public void 湧き点から敵を生成し足場の上に立たせる()
        {
            var world = NewWorld();

            Assert.AreEqual(2, world.Enemies.Count);
            var slime = world.Enemies[0];
            Assert.AreEqual("Slime", slime.Definition.Name);
            Assert.AreEqual(3, slime.Ground!.Id);
            Assert.AreEqual(25f, slime.X);
            Assert.AreEqual(5f, slime.Y);
            Assert.AreEqual(10, slime.Hp);
            Assert.AreEqual(1, slime.InstanceId);
            Assert.AreEqual(2, world.Enemies[1].InstanceId);
        }

        [Test]
        public void 定義の無い敵IDは仮の定義で湧く()
        {
            var map = TestMaps.Sample();
            map.SpawnPoints[0].EnemyId = 99;
            var world = NewWorld(map);

            Assert.AreEqual("enemy99", world.Enemies[0].Definition.Name);
        }

        [Test]
        public void 攻撃でHPが減り0で死亡してドロップし時間が経つと復活する()
        {
            var world = NewWorld();
            var slime = world.Enemies[0];
            var died = 0;
            var respawned = 0;
            world.EnemyDied += _ => died++;
            world.EnemyRespawned += _ => respawned++;

            var first = world.PlayerAttack(24f, 5f, Direction.Right, 1.6f, 1.2f, 6);
            Assert.IsTrue(first.Hit);
            Assert.AreSame(slime, first.Target);
            Assert.IsFalse(first.Killed);
            Assert.AreEqual(4, slime.Hp);

            var second = world.PlayerAttack(24f, 5f, Direction.Right, 1.6f, 1.2f, 6);
            Assert.IsTrue(second.Killed);
            Assert.AreEqual(new[] { 1, 4 }, second.DroppedItemIds);
            Assert.IsTrue(slime.IsDead);
            Assert.AreEqual(0, slime.Hp);
            Assert.AreEqual(1, died);
            Assert.AreEqual(2, world.Drops.Count);
            Assert.AreEqual(5f, world.Drops[0].Y);

            Assert.IsFalse(world.PlayerAttack(24f, 5f, Direction.Right, 1.6f, 1.2f, 6).Hit, "死亡中は当たらない");

            world.Tick(9.9f);
            Assert.IsTrue(slime.IsDead);
            Assert.AreEqual(0, respawned);

            world.Tick(0.2f);
            Assert.IsFalse(slime.IsDead);
            Assert.AreEqual(10, slime.Hp);
            Assert.AreEqual(25f, slime.X);
            Assert.AreEqual(1, respawned);
        }

        [Test]
        public void 攻撃は向いている方向の範囲内の敵だけに当たる()
        {
            var world = NewWorld();

            Assert.IsFalse(world.PlayerAttack(26f, 5f, Direction.Right, 1.6f, 1.2f, 1).Hit, "敵が後ろにいる");
            Assert.IsTrue(world.PlayerAttack(26f, 5f, Direction.Left, 1.6f, 1.2f, 1).Hit, "振り向けば当たる");
            Assert.IsFalse(world.PlayerAttack(22f, 5f, Direction.Right, 1.6f, 1.2f, 1).Hit, "遠すぎる");
            Assert.IsFalse(world.PlayerAttack(24f, 8f, Direction.Right, 1.6f, 1.2f, 1).Hit, "高さが違う");
        }

        [Test]
        public void 複数いれば一番近い敵に当たる()
        {
            var map = TestMaps.Sample();
            map.SpawnPoints.Add(new SpawnPoint { Id = 3, X = 26f, Y = 5f, EnemyId = 2, RespawnSeconds = 10 });
            var world = NewWorld(map);

            var outcome = world.PlayerAttack(24f, 5f, Direction.Right, 3f, 1.2f, 1);

            Assert.AreEqual("Slime", outcome.Target!.Definition.Name);
        }

        [Test]
        public void ドロップ率0なら何も落とさない()
        {
            var world = new LocalWorld(TestMaps.Sample(), id => new EnemyDefinition { EnemyId = id, Name = "x", MaxHp = 1, DropItemIds = new[] { 1 }, DropRate = 0f }, new Random(1));
            world.SpawnFromMap();

            var outcome = world.PlayerAttack(24f, 5f, Direction.Right, 1.6f, 1.2f, 1);

            Assert.IsTrue(outcome.Killed);
            Assert.IsEmpty(outcome.DroppedItemIds);
            Assert.IsEmpty(world.Drops);
        }

        [Test]
        public void 巡回は足場の端で折り返す()
        {
            var world = NewWorld();
            var slime = world.Enemies[0]; // #3 (x 20..30) の x=25 から左へ 1.5/s

            for (var i = 0; i < 4 * 60; i++) world.Tick(1f / 60f);

            Assert.AreEqual(Direction.Right, slime.Facing, "左端 (x=20) で折り返している");
            Assert.GreaterOrEqual(slime.X, 20f);
            Assert.LessOrEqual(slime.X, 30f);
            Assert.AreEqual(5f, slime.Y, 0.001f);
        }

        [Test]
        public void 接触判定は足元基準の箱の重なりで判定する()
        {
            var world = NewWorld();

            Assert.IsNotNull(world.FindTouchingEnemy(24.5f, 5f, 0.35f, 1.5f));
            Assert.IsNull(world.FindTouchingEnemy(23f, 5f, 0.35f, 1.5f), "横に離れている");
            Assert.IsNull(world.FindTouchingEnemy(25f, 7f, 0.35f, 1.5f), "上に離れている");
            Assert.IsNotNull(world.FindTouchingEnemy(25f, 4.2f, 0.35f, 1.5f), "少し下でも重なる");
        }

        [Test]
        public void ドロップは範囲内なら拾え時間が経つと消える()
        {
            var world = NewWorld();
            world.PlayerAttack(24f, 5f, Direction.Right, 1.6f, 1.2f, 99);
            Assert.AreEqual(2, world.Drops.Count);

            Assert.IsNull(world.TryPickup(20f, 5f, 1f), "遠い");
            var picked = world.TryPickup(25f, 5f, 1f);
            Assert.IsNotNull(picked);
            Assert.AreEqual(1, world.Drops.Count);

            world.Tick(LocalWorld.DropLifetimeSeconds + 1f);
            Assert.IsEmpty(world.Drops);
        }
    }
}
