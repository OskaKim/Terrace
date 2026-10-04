using System;
using System.Collections.Generic;
using NUnit.Framework;
using Terrace.Client.Core;
using Terrace.Map;

namespace Terrace.Client.Tests.EditMode
{
    /// <summary>オフラインの世界の権威(Server の Room と同じ規則で、このクライアントが決める)。</summary>
    [TestFixture]
    public class OfflineRoomTests
    {
        private static OfflineRoom NewRoom(MapData? map = null)
        {
            var room = new OfflineRoom(map ?? TestMaps.Sample(), TestMaps.Lookup, new Random(1));
            room.SpawnFromMap();
            return room;
        }

        /// <summary>向いている方向の一番近い敵を探して攻撃を頼む。当たった敵を返す(外れなら null)。</summary>
        private static EnemyEntity? Attack(OfflineRoom room, float x, float y, Direction facing, float range, float height, int damage)
        {
            var target = room.State.FindAttackTarget(x, y, facing, range, height);
            if (target != null) room.RequestAttack(target, damage);
            return target;
        }

        [Test]
        public void 湧き点から敵を生成し足場の上に立たせる()
        {
            var room = NewRoom();

            Assert.AreEqual(2, room.State.Enemies.Count);
            var slime = room.State.Enemies[0];
            Assert.AreEqual("Slime", slime.Definition.Name);
            Assert.AreEqual(3, room.GroundOf(slime)!.Id);
            Assert.AreEqual(25f, slime.X);
            Assert.AreEqual(5f, slime.Y);
            Assert.AreEqual(10, slime.Hp);
            Assert.AreEqual(1, slime.InstanceId);
            Assert.AreEqual(2, room.State.Enemies[1].InstanceId);
        }

        [Test]
        public void 定義の無い敵IDは仮の定義で湧く()
        {
            var map = TestMaps.Sample();
            map.SpawnPoints[0].EnemyId = 99;
            var room = NewRoom(map);

            Assert.AreEqual("enemy99", room.State.Enemies[0].Definition.Name);
        }

        [Test]
        public void 攻撃でHPが減り0で死亡してドロップし時間が経つと復活する()
        {
            var room = NewRoom();
            var slime = room.State.Enemies[0];
            var kills = new List<EnemyKill>();
            var respawned = 0;
            room.EnemyKilled += kills.Add;
            room.EnemySpawned += _ => respawned++;

            Assert.AreSame(slime, Attack(room, 24f, 5f, Direction.Right, 1.6f, 1.2f, 6));
            Assert.IsEmpty(kills);
            Assert.AreEqual(4, slime.Hp);

            Attack(room, 24f, 5f, Direction.Right, 1.6f, 1.2f, 6);
            Assert.AreEqual(1, kills.Count);
            Assert.AreEqual(new[] { 1, 4 }, kills[0].DroppedItemIds);
            Assert.IsTrue(slime.IsDead);
            Assert.AreEqual(0, slime.Hp);
            Assert.AreEqual(2, room.State.Drops.Count);
            Assert.AreEqual(5f, room.State.Drops[0].Y);

            Assert.IsNull(Attack(room, 24f, 5f, Direction.Right, 1.6f, 1.2f, 6), "死亡中は当たらない");

            room.Tick(9.9f);
            Assert.IsTrue(slime.IsDead);
            Assert.AreEqual(0, respawned);

            room.Tick(0.2f);
            Assert.IsFalse(slime.IsDead);
            Assert.AreEqual(10, slime.Hp);
            Assert.AreEqual(25f, slime.X);
            Assert.AreEqual(1, respawned);
        }

        [Test]
        public void 倒すとその場で倒れた自分と落とし物が出たが起きる()
        {
            var room = NewRoom();
            var slime = room.State.Enemies[0];
            var events = new List<string>();
            room.EnemyDamaged += d => events.Add($"damaged {d.Enemy.InstanceId} by {d.Attacker}");
            room.DropSpawned += d => events.Add($"drop {d.ItemId}");
            room.EnemyKilled += k => events.Add($"killed {k.Enemy.InstanceId} by {k.Killer}");

            room.RequestAttack(slime, 99);

            Assert.AreEqual(new[] { "damaged 1 by self", "drop 1", "drop 4", "killed 1 by self" }, events.ToArray());
        }

        [Test]
        public void 攻撃は向いている方向の範囲内の敵だけに当たる()
        {
            var room = NewRoom();

            Assert.IsNull(Attack(room, 26f, 5f, Direction.Right, 1.6f, 1.2f, 1), "敵が後ろにいる");
            Assert.IsNotNull(Attack(room, 26f, 5f, Direction.Left, 1.6f, 1.2f, 1), "振り向けば当たる");
            Assert.IsNull(Attack(room, 22f, 5f, Direction.Right, 1.6f, 1.2f, 1), "遠すぎる");
            Assert.IsNull(Attack(room, 24f, 8f, Direction.Right, 1.6f, 1.2f, 1), "高さが違う");
        }

        [Test]
        public void 複数いれば一番近い敵に当たる()
        {
            var map = TestMaps.Sample();
            map.SpawnPoints.Add(new SpawnPoint { Id = 3, X = 26f, Y = 5f, EnemyId = 2, RespawnSeconds = 10 });
            var room = NewRoom(map);

            var target = Attack(room, 24f, 5f, Direction.Right, 3f, 1.2f, 1);

            Assert.AreEqual("Slime", target!.Definition.Name);
        }

        [Test]
        public void ドロップ率0なら何も落とさない()
        {
            var room = new OfflineRoom(TestMaps.Sample(), id => new EnemyDefinition { EnemyId = id, Name = "x", MaxHp = 1, DropItemIds = new[] { 1 }, DropRate = 0f }, new Random(1));
            room.SpawnFromMap();
            var kills = new List<EnemyKill>();
            room.EnemyKilled += kills.Add;

            Attack(room, 24f, 5f, Direction.Right, 1.6f, 1.2f, 1);

            Assert.AreEqual(1, kills.Count);
            Assert.IsEmpty(kills[0].DroppedItemIds);
            Assert.IsEmpty(room.State.Drops);
        }

        [Test]
        public void 巡回は足場の端で折り返す()
        {
            var room = NewRoom();
            var slime = room.State.Enemies[0]; // #3 (x 20..30) の x=25 から左へ 1.5/s

            for (var i = 0; i < 4 * 60; i++) room.Tick(1f / 60f);

            Assert.AreEqual(Direction.Right, slime.Facing, "左端 (x=20) で折り返している");
            Assert.GreaterOrEqual(slime.X, 20f);
            Assert.LessOrEqual(slime.X, 30f);
            Assert.AreEqual(5f, slime.Y, 0.001f);
        }

        [Test]
        public void 接触判定は足元基準の箱の重なりで判定する()
        {
            var world = NewRoom().State;

            Assert.IsNotNull(world.FindTouchingEnemy(24.5f, 5f, 0.35f, 1.5f));
            Assert.IsNull(world.FindTouchingEnemy(23f, 5f, 0.35f, 1.5f), "横に離れている");
            Assert.IsNull(world.FindTouchingEnemy(25f, 7f, 0.35f, 1.5f), "上に離れている");
            Assert.IsNotNull(world.FindTouchingEnemy(25f, 4.2f, 0.35f, 1.5f), "少し下でも重なる");
        }

        [Test]
        public void ドロップは範囲内なら拾え時間が経つと消える()
        {
            var room = NewRoom();
            var removals = new List<DropRemoval>();
            room.DropRemoved += removals.Add;
            Attack(room, 24f, 5f, Direction.Right, 1.6f, 1.2f, 99);
            Assert.AreEqual(2, room.State.Drops.Count);

            Assert.IsNull(room.State.FindPickupCandidate(20f, 5f, 1f), "遠い");
            var candidate = room.State.FindPickupCandidate(25f, 5f, 1f);
            Assert.IsNotNull(candidate);
            room.RequestPickup(candidate!);
            Assert.AreEqual(1, room.State.Drops.Count);
            Assert.AreSame(candidate, removals[0].Drop);
            Assert.IsTrue(removals[0].Picker.IsSelf, "オフラインで拾うのは自分");

            room.Tick(WorldState.DropLifetimeSeconds + 1f);
            Assert.IsEmpty(room.State.Drops);
            Assert.IsFalse(removals[1].Picker.IsSelf, "時間切れは誰でもない");
        }
    }
}
