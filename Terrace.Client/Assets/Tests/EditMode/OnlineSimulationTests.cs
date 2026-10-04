using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Terrace.Client.Core;
using Terrace.Client.Core.Online;
using Terrace.Map;
using Terrace.Shared;

namespace Terrace.Client.Tests.EditMode
{
    /// <summary>
    /// オンラインの GameSimulation。通信はせず、送信は FakeChannel に記録し、サーバーの通知は OnlineInbox に直接積む。
    /// </summary>
    [TestFixture]
    public class OnlineSimulationTests
    {
        private const float Dt = 1f / 60f;
        private const int SelfId = 1;
        private static readonly PlayerInfo Bob = new PlayerInfo { PlayerId = 2, Name = "bob" };

        private sealed class FakeChannel : IOnlineChannel
        {
            public PlayerInfo Self { get; } = new PlayerInfo { PlayerId = SelfId, Name = "alice" };
            public string ServerAddress => "fake://server";
            public List<(int MapId, MoveState State)> Joins { get; } = new List<(int, MoveState)>();
            public List<MoveState> Moves { get; } = new List<MoveState>();
            public List<(int EnemyInstanceId, int Damage)> Attacks { get; } = new List<(int, int)>();
            public List<int> Pickups { get; } = new List<int>();

            public void Join(int mapId, MoveState state) => Joins.Add((mapId, state));
            public void Move(MoveState state) => Moves.Add(state);
            public void Attack(int enemyInstanceId, int damage) => Attacks.Add((enemyInstanceId, damage));
            public void Pickup(int dropId) => Pickups.Add(dropId);
        }

        private static (GameSimulation Sim, FakeChannel Channel, IGameHubReceiver Server, OnlineInbox Inbox) Online(MapData start, params MapData[] others)
            => Online(null, start, others);

        private static (GameSimulation Sim, FakeChannel Channel, IGameHubReceiver Server, OnlineInbox Inbox) Online(LevelTable? levels, MapData start, params MapData[] others)
        {
            var maps = new List<MapData> { start };
            maps.AddRange(others);
            var channel = new FakeChannel();
            var inbox = new OnlineInbox();
            var sim = new GameSimulation(
                start, TestMaps.Lookup, TestMaps.ItemName,
                random: new Random(1),
                mapLookup: id => maps.FirstOrDefault(m => m.Id == id),
                items: TestMaps.Catalog,
                shops: new PlaceholderShopCatalog(TestMaps.Catalog),
                online: channel,
                inbox: inbox,
                levels: levels);
            return (sim, channel, inbox, inbox);
        }

        private static EnemyState Slime(int instanceId, float x, int hp = 10, bool dead = false)
            => new EnemyState { InstanceId = instanceId, EnemyId = 1, X = x, Y = 0f, Hp = hp, MaxHp = 10, IsDead = dead };

        private static RoomSnapshot Snapshot(int mapId, EnemyState[]? enemies = null, DropState[]? drops = null, params PlayerSnapshot[] players)
            => new RoomSnapshot
            {
                MapId = mapId,
                Enemies = enemies ?? Array.Empty<EnemyState>(),
                Drops = drops ?? Array.Empty<DropState>(),
                Players = players,
            };

        private static void Run(GameSimulation sim, InputFrame input, float seconds)
        {
            var steps = (int)(seconds / Dt + 0.5f);
            for (var i = 0; i < steps; i++) sim.Step(input, Dt);
        }

        [Test]
        public void 始めると今のマップに参加を送りスナップショットで敵と他のプレイヤーが映る()
        {
            var (sim, channel, server, _) = Online(TestMaps.Field());

            Assert.IsTrue(sim.IsOnline);
            Assert.AreEqual(1, channel.Joins.Count);
            Assert.AreEqual(1, channel.Joins[0].MapId);
            Assert.AreEqual(5f, channel.Joins[0].State.X, 0.001f, "出現地点から参加する");
            Assert.IsInstanceOf<RoomMirror>(sim.WorldAuthority);
            Assert.IsEmpty(sim.World.Enemies, "敵は自分で湧かせない");
            Assert.IsFalse(sim.IsSynchronized);

            server.OnSnapshot(Snapshot(1,
                new[] { Slime(7, 20f, hp: 6) },
                new[] { new DropState { DropId = 3, ItemId = 4, X = 12f, Y = 0f } },
                new PlayerSnapshot { Info = Bob, State = new MoveState { X = 10f, Facing = Facing.Left } }));
            sim.Step(InputFrame.None, Dt);

            Assert.IsTrue(sim.IsSynchronized);
            var slime = sim.World.Enemies.Single();
            Assert.AreEqual((7, 6, 0.6f), (slime.InstanceId, slime.Hp, slime.HpRatio));
            Assert.AreEqual(3, sim.World.Drops.Single().DropId);
            var bob = sim.RemotePlayers.All.Single();
            Assert.AreEqual(("bob", 10f, Direction.Left), (bob.Name, bob.X, bob.Facing));
            Assert.IsTrue(sim.Messages.Contains("ほかに 1 人"));
        }

        [Test]
        public void スナップショットの前に届いた通知と別のマップのスナップショットは捨てる()
        {
            var (sim, _, server, _) = Online(TestMaps.Field());

            server.OnEnemySpawn(Slime(1, 20f));
            server.OnJoin(Bob, new MoveState());
            server.OnSnapshot(Snapshot(2, new[] { Slime(9, 3f) }));
            sim.Step(InputFrame.None, Dt);

            Assert.IsFalse(sim.IsSynchronized);
            Assert.IsEmpty(sim.World.Enemies);
            Assert.AreEqual(0, sim.RemotePlayers.Count);
        }

        [Test]
        public void 攻撃は当たった敵とダメージを送るだけでHPと撃破と報酬はサーバーの通知で決まる()
        {
            var (sim, channel, server, _) = Online(TestMaps.Field());
            server.OnSnapshot(Snapshot(1, new[] { Slime(7, 6f) }));
            sim.Step(InputFrame.None, Dt);
            var slime = sim.World.Enemies.Single();
            var mesoBefore = sim.Player.Meso;

            sim.Step(InputFrame.None.WithAttack(), Dt);

            Assert.AreEqual((7, sim.Player.Attack), channel.Attacks.Single());
            Assert.AreEqual(10, slime.Hp, "自分では減らさない");

            server.OnEnemyDamaged(7, 3, SelfId, 7);
            sim.Step(InputFrame.None, Dt);
            Assert.AreEqual(3, slime.Hp);
            Assert.IsTrue(sim.Messages.Contains("Slime に 7 ダメージ (残り 3)"));

            server.OnDropSpawn(new[] { new DropState { DropId = 5, ItemId = 1, X = 6f, Y = 0f } });
            server.OnEnemyDamaged(7, 0, SelfId, 3);
            server.OnEnemyDead(7, SelfId, new[] { 1 });
            sim.Step(InputFrame.None, Dt);

            Assert.IsTrue(slime.IsDead);
            Assert.AreEqual(1, sim.Player.Kills);
            Assert.AreEqual(mesoBefore + 10, sim.Player.Meso, "報酬は MaxHp と同じメソ");
            Assert.AreEqual(1, sim.World.Drops.Count);
            Assert.IsTrue(sim.Messages.Contains("Slime を倒した (+10 メソ, +3 EXP) ドロップ: Potion"));

            // 同じ撃破の通知が重ねて届いても二重に報酬を得ない
            server.OnEnemyDead(7, SelfId, new[] { 1 });
            sim.Step(InputFrame.None, Dt);
            Assert.AreEqual(1, sim.Player.Kills);

            // 復活の通知で生き返る
            server.OnEnemySpawn(Slime(7, 20f));
            sim.Step(InputFrame.None, Dt);
            Assert.IsFalse(slime.IsDead);
            Assert.AreEqual(10, slime.Hp);
            Assert.AreEqual(20f, slime.X, 0.001f);
        }

        [Test]
        public void 撃破の通知で倒した人が自分ならEnemyKilledが1回起き他人なら起きない()
        {
            var (sim, _, server, _) = Online(TestMaps.Field());
            server.OnSnapshot(Snapshot(1, new[] { Slime(7, 6f), Slime(8, 20f) }));
            sim.Step(InputFrame.None, Dt);
            var killed = new List<int>();
            sim.EnemyKilled += enemy => killed.Add(enemy.InstanceId);

            server.OnEnemyDead(8, Bob.PlayerId, Array.Empty<int>());
            sim.Step(InputFrame.None, Dt);
            Assert.IsEmpty(killed, "他の人が倒した");

            server.OnEnemyDead(7, SelfId, Array.Empty<int>());
            server.OnEnemyDead(7, SelfId, Array.Empty<int>()); // 同じ通知が重ねて届いても 1 回
            sim.Step(InputFrame.None, Dt);
            Assert.AreEqual(new[] { 7 }, killed);
        }

        [Test]
        public void 撃破の通知で倒した人が自分なら経験値を得て他人なら得ない()
        {
            var (sim, _, server, _) = Online(TestMaps.Field());
            server.OnSnapshot(Snapshot(1, new[] { Slime(7, 6f), Slime(8, 20f) }));
            sim.Step(InputFrame.None, Dt);

            server.OnEnemyDead(8, Bob.PlayerId, Array.Empty<int>());
            sim.Step(InputFrame.None, Dt);
            Assert.AreEqual(0, sim.Player.Exp, "他の人が倒した");

            server.OnEnemyDead(7, SelfId, Array.Empty<int>());
            server.OnEnemyDead(7, SelfId, Array.Empty<int>()); // 同じ通知が重ねて届いても 1 回
            sim.Step(InputFrame.None, Dt);
            Assert.AreEqual(3, sim.Player.Exp, "Slime の Exp");
        }

        [Test]
        public void オンラインでもレベルが上がるとそのレベルの攻撃力をダメージとして送る()
        {
            var (sim, channel, server, _) = Online(TestMaps.Levels(), TestMaps.Field());
            server.OnSnapshot(Snapshot(1, new[] { Slime(7, 6f), Slime(8, 20f) }));
            sim.Step(InputFrame.None, Dt);
            var leveled = new List<int>();
            sim.LeveledUp += level => leveled.Add(level);

            server.OnEnemyDead(7, SelfId, Array.Empty<int>());
            server.OnEnemyDead(8, SelfId, Array.Empty<int>());
            sim.Step(InputFrame.None, Dt);
            Assert.AreEqual((2, 1), (sim.Player.Level, sim.Player.Exp), "Slime 2 体で 6、レベル 1 の ExpToNext は 5");
            Assert.AreEqual(new[] { 2 }, leveled);
            Assert.IsTrue(sim.Messages.Contains("レベルが上がった! Lv. 2"));

            server.OnEnemySpawn(Slime(7, 6f));
            sim.Step(InputFrame.None, Dt);
            sim.Step(InputFrame.None.WithAttack(), Dt);

            Assert.AreEqual((7, 14), channel.Attacks.Single(), "レベル 2 の Attack");
        }

        [Test]
        public void 他の人が倒した敵では報酬を得ない()
        {
            var (sim, _, server, _) = Online(TestMaps.Field());
            server.OnSnapshot(Snapshot(1, new[] { Slime(7, 20f) }, null, new PlayerSnapshot { Info = Bob, State = new MoveState { X = 19f } }));
            sim.Step(InputFrame.None, Dt);

            server.OnEnemyDamaged(7, 0, Bob.PlayerId, 10);
            server.OnEnemyDead(7, Bob.PlayerId, Array.Empty<int>());
            sim.Step(InputFrame.None, Dt);

            Assert.IsTrue(sim.World.Enemies.Single().IsDead);
            Assert.AreEqual(0, sim.Player.Kills);
            Assert.IsTrue(sim.Messages.Contains("bob が Slime を倒した"));
        }

        [Test]
        public void 拾うと要求を送り自分が拾った通知で持ち物に入り他の人が拾えば消えるだけ()
        {
            var (sim, channel, server, _) = Online(TestMaps.Field());
            server.OnSnapshot(Snapshot(1, null, new[]
            {
                new DropState { DropId = 1, ItemId = 4, X = 5.2f, Y = 0f },
                new DropState { DropId = 2, ItemId = 1, X = 5.6f, Y = 0f },
            }));
            sim.Step(InputFrame.None, Dt);

            sim.Step(InputFrame.None.WithPickup(), Dt);
            sim.Step(InputFrame.None.WithPickup(), Dt);

            Assert.AreEqual(new[] { 1 }, channel.Pickups.ToArray(), "近い方を 1 回だけ要求する(返事を待つ間は重ねて送らない)");
            Assert.IsEmpty(sim.Player.Inventory, "返事が来るまでは持ち物に入れない");

            server.OnDropRemoved(1, SelfId);
            server.OnDropRemoved(2, Bob.PlayerId);
            sim.Step(InputFrame.None, Dt);

            Assert.AreEqual(new[] { 4 }, sim.Player.Inventory.ToArray());
            Assert.IsEmpty(sim.World.Drops);
            Assert.IsTrue(sim.Messages.Contains("Herb を拾った"));
        }

        [Test]
        public void 他のプレイヤーの参加と移動と退出が映り表示位置は滑らかに追いつく()
        {
            var (sim, _, server, _) = Online(TestMaps.Field());
            server.OnSnapshot(Snapshot(1));
            sim.Step(InputFrame.None, Dt);
            var added = new List<string>();
            var removed = new List<string>();
            sim.RemotePlayers.Added += p => added.Add(p.Name);
            sim.RemotePlayers.Removed += p => removed.Add(p.Name);

            server.OnJoin(Bob, new MoveState { X = 10f });
            server.OnJoin(new PlayerInfo { PlayerId = SelfId, Name = "alice" }, new MoveState());
            sim.Step(InputFrame.None, Dt);
            Assert.AreEqual(new[] { "bob" }, added.ToArray(), "自分自身の参加は数えない");
            Assert.IsTrue(sim.Messages.Contains("bob がやって来た"));

            server.OnMove(Bob.PlayerId, new MoveState { X = 12f, VelocityX = 3f, Motion = MotionState.Walk, Facing = Facing.Right });
            sim.Step(InputFrame.None, Dt);
            var bob = sim.RemotePlayers.Find(Bob.PlayerId)!;
            Assert.Greater(bob.X, 10f);
            Assert.Less(bob.X, 11f, "一気には飛ばない");

            Run(sim, InputFrame.None, 1f);
            Assert.AreEqual(12f + 3f * RemotePlayer.MaxLead, bob.X, 0.05f, "歩いている向きに少し先読みした位置へ追いつく");
            Assert.AreEqual(MotionState.Walk, bob.Motion);

            server.OnMove(Bob.PlayerId, new MoveState { X = 35f, Motion = MotionState.Stand });
            sim.Step(InputFrame.None, Dt);
            Assert.AreEqual(35f, bob.X, 0.001f, "遠く離れたら瞬間移動");

            server.OnLeave(Bob.PlayerId);
            sim.Step(InputFrame.None, Dt);
            Assert.AreEqual(0, sim.RemotePlayers.Count);
            Assert.AreEqual(new[] { "bob" }, removed.ToArray());
            Assert.IsTrue(sim.Messages.Contains("bob が去った"));
        }

        [Test]
        public void 移動は間引いて送り向きや状態が変わればすぐ送る()
        {
            var (sim, channel, server, _) = Online(TestMaps.Field());
            server.OnSnapshot(Snapshot(1));
            Run(sim, InputFrame.None, 1.2f);
            channel.Moves.Clear();

            Run(sim, InputFrame.Hold(right: true), 1f);
            Assert.That(channel.Moves.Count, Is.InRange(9, 13), "歩いている間は 0.1 秒ごと");
            Assert.AreEqual(MotionState.Walk, channel.Moves[0].Motion, "歩き始めはすぐ送る");
            Assert.AreEqual(Facing.Right, channel.Moves.Last().Facing);

            channel.Moves.Clear();
            sim.Step(InputFrame.Hold(left: true), Dt);
            sim.Step(InputFrame.Hold(left: true), Dt);
            Assert.AreEqual(Facing.Left, channel.Moves.Last().Facing, "向きが変わったらすぐ送る");

            channel.Moves.Clear();
            Run(sim, InputFrame.None, 3f);
            Assert.That(channel.Moves.Count, Is.InRange(2, 6), "止まっている間は 1 秒ごとの生存報告だけ");
            Assert.AreEqual(MotionState.Stand, channel.Moves.Last().Motion);
        }

        [Test]
        public void 状態は梯子と攻撃と死亡を区別して送る()
        {
            var (sim, _, _, _) = Online(TestMaps.Field());
            Assert.AreEqual(MotionState.Stand, sim.CurrentMoveState().Motion);

            sim.Step(InputFrame.None.WithAttack(), Dt);
            Assert.AreEqual(MotionState.Attack, sim.CurrentMoveState().Motion);

            Run(sim, InputFrame.None, 1f);
            sim.Motor.Teleport(20f, 5f);
            Assert.AreEqual(MotionState.Jump, sim.CurrentMoveState().Motion, "空中");
        }

        [Test]
        public void ポータルでマップを移ると参加し直し前のマップの通知は捨てる()
        {
            var (sim, channel, server, _) = Online(TestMaps.Field(), TestMaps.Town());
            server.OnSnapshot(Snapshot(1, new[] { Slime(7, 20f) }, null, new PlayerSnapshot { Info = Bob, State = new MoveState { X = 30f } }));
            sim.Step(InputFrame.None, Dt);
            Assert.AreEqual(1, sim.RemotePlayers.Count);

            sim.Motor.Teleport(38f, 0f);
            Run(sim, InputFrame.None, 1.1f);
            sim.Step(InputFrame.Hold(up: true), Dt);

            Assert.AreEqual(100, sim.Map.Id);
            Assert.AreEqual(100, channel.Joins.Last().MapId);
            Assert.AreEqual(2f, channel.Joins.Last().State.X, 0.001f, "町の west_gate から参加する");
            Assert.AreEqual(0, sim.RemotePlayers.Count, "前のマップの人は消える");
            Assert.IsEmpty(sim.World.Enemies);
            Assert.IsFalse(sim.IsSynchronized);

            // 前のマップ(狩場)の通知が遅れて届いても映さない
            server.OnMove(Bob.PlayerId, new MoveState { X = 31f });
            server.OnEnemySpawn(Slime(8, 10f));
            sim.Step(InputFrame.None, Dt);
            Assert.AreEqual(0, sim.RemotePlayers.Count);
            Assert.IsEmpty(sim.World.Enemies);

            server.OnSnapshot(Snapshot(100));
            sim.Step(InputFrame.None, Dt);
            Assert.IsTrue(sim.IsSynchronized);
        }

        [Test]
        public void 切断されたらオフラインに切り替わり今のマップの敵を自分で湧かせる()
        {
            var (sim, _, server, inbox) = Online(TestMaps.Field());
            server.OnSnapshot(Snapshot(1, new[] { Slime(7, 20f) }, null, new PlayerSnapshot { Info = Bob, State = new MoveState { X = 30f } }));
            sim.Step(InputFrame.None, Dt);
            string? reason = null;
            WorldState? replaced = null;
            sim.WentOffline += r => reason = r;
            sim.WorldReplaced += w => replaced = w;

            inbox.MarkDisconnected("テスト");
            sim.Step(InputFrame.None, Dt);

            Assert.IsFalse(sim.IsOnline);
            Assert.AreEqual("テスト", reason);
            Assert.AreSame(sim.World, replaced);
            Assert.IsInstanceOf<OfflineRoom>(sim.WorldAuthority);
            Assert.AreEqual(1, sim.World.Enemies.Count, "マップの湧き点から湧く");
            Assert.AreEqual(0, sim.RemotePlayers.Count);
            Assert.IsTrue(sim.Messages.Contains("オフラインで続けます"));

            // オフラインでは攻撃で HP が減る
            sim.Motor.Teleport(19f, 0f);
            Run(sim, InputFrame.None, 1.1f);
            sim.Step(InputFrame.None.WithAttack(), Dt);
            Assert.IsTrue(sim.World.Enemies[0].IsDead);
        }

        [Test]
        public void サーバーの敵の位置へ滑らかに寄せ大きく離れたら瞬間移動する()
        {
            var mirror = new RoomMirror(TestMaps.Field(), TestMaps.Lookup, new FakeChannel());
            mirror.ApplySnapshot(new[] { Slime(1, 20f) }, Array.Empty<DropState>());
            var slime = mirror.State.Enemies[0];

            mirror.ApplyEnemyMove(new[] { new EnemyMoveState { InstanceId = 1, X = 21f, Y = 0f, Facing = Facing.Right } });
            mirror.Tick(0.05f);
            Assert.Greater(slime.X, 20f);
            Assert.Less(slime.X, 21f);
            Assert.AreEqual(Direction.Right, slime.Facing);
            for (var i = 0; i < 60; i++) mirror.Tick(Dt);
            Assert.AreEqual(21f, slime.X, 0.01f);

            mirror.ApplyEnemyMove(new[] { new EnemyMoveState { InstanceId = 1, X = 35f, Y = 0f } });
            mirror.Tick(Dt);
            Assert.AreEqual(35f, slime.X, 0.001f);

            // サーバー権威の世界では自分で巡回も復活もしない
            mirror.ApplyEnemyDead(1, Bob.PlayerId, Array.Empty<int>());
            for (var i = 0; i < 600; i++) mirror.Tick(1f);
            Assert.IsTrue(slime.IsDead);
        }

        [Test]
        public void RoomMirrorは攻撃を送るだけで結果のイベントは撃破の通知を映したときに出る()
        {
            var channel = new FakeChannel();
            var mirror = new RoomMirror(TestMaps.Field(), TestMaps.Lookup, channel);
            mirror.ApplySnapshot(new[] { Slime(7, 6f) }, Array.Empty<DropState>());
            var slime = mirror.State.Enemies.Single();
            var damages = new List<EnemyDamage>();
            var kills = new List<EnemyKill>();
            mirror.EnemyDamaged += damages.Add;
            mirror.EnemyKilled += kills.Add;

            mirror.RequestAttack(slime, 4);

            Assert.AreEqual((7, 4), channel.Attacks.Single(), "当たった敵とダメージを送る");
            Assert.AreEqual(10, slime.Hp, "自分では減らさない");
            Assert.Greater(slime.HitFlash, 0f, "被弾表示だけ先に出す");
            Assert.IsEmpty(damages);
            Assert.IsEmpty(kills);

            mirror.ApplyEnemyDead(7, SelfId, new[] { 1 });
            Assert.AreEqual(1, kills.Count);
            Assert.IsTrue(kills[0].Killer.IsSelf, "倒した人が自分");
            Assert.AreEqual(new[] { 1 }, kills[0].DroppedItemIds);

            mirror.ApplyEnemyDead(7, SelfId, new[] { 1 });
            Assert.AreEqual(1, kills.Count, "重なって届いた撃破の通知では起きない");
        }

        [Test]
        public void RoomMirrorで他人が倒した通知では倒した人が他人になる()
        {
            var mirror = new RoomMirror(TestMaps.Field(), TestMaps.Lookup, new FakeChannel());
            mirror.ApplySnapshot(new[] { Slime(8, 20f) }, Array.Empty<DropState>());
            var kills = new List<EnemyKill>();
            mirror.EnemyKilled += kills.Add;

            mirror.ApplyEnemyDead(8, Bob.PlayerId, Array.Empty<int>());

            Assert.IsFalse(kills.Single().Killer.IsSelf);
            Assert.AreEqual(Bob.PlayerId, kills.Single().Killer.PlayerId);
        }

        [Test]
        public void 受け箱は別のスレッドから積まれた通知を順番どおりに渡す()
        {
            var inbox = new OnlineInbox();
            IGameHubReceiver server = inbox;
            Task.Run(() =>
            {
                for (var i = 0; i < 1000; i++) server.OnMove(2, new MoveState { X = i });
            }).Wait();

            var recorder = new MoveRecorder();
            Assert.AreEqual(1000, inbox.Drain(recorder));
            Assert.AreEqual(Enumerable.Range(0, 1000).Select(i => (float)i).ToArray(), recorder.Xs.ToArray());
            Assert.AreEqual(0, inbox.Pending);
        }

        [Test]
        public void MoveSenderは最初と状態の変化と一定間隔で送る()
        {
            var sender = new MoveSender();
            var stand = new MoveState { X = 0f, Motion = MotionState.Stand };
            Assert.IsTrue(sender.ShouldSend(stand, 0f), "最初は必ず送る");
            sender.MarkSent(stand);

            Assert.IsFalse(sender.ShouldSend(stand, 0.5f));
            Assert.IsTrue(sender.ShouldSend(stand, 0.6f), "1 秒経てば生存報告");
            sender.MarkSent(stand);

            var walk = new MoveState { X = 0.01f, Motion = MotionState.Walk };
            Assert.IsFalse(sender.ShouldSend(walk, 0.01f), "変化があっても間隔が短すぎれば待つ");
            Assert.IsTrue(sender.ShouldSend(walk, 0.03f), "状態が変わったらすぐ");
            sender.MarkSent(walk);

            var moved = new MoveState { X = 0.5f, Motion = MotionState.Walk };
            Assert.IsFalse(sender.ShouldSend(moved, 0.05f));
            Assert.IsTrue(sender.ShouldSend(moved, 0.06f), "位置だけの変化は 0.1 秒ごと");
        }

        private sealed class MoveRecorder : IGameHubReceiver
        {
            public List<float> Xs { get; } = new List<float>();
            public void OnMove(int playerId, MoveState state) => Xs.Add(state.X);
            public void OnJoin(PlayerInfo player, MoveState state) { }
            public void OnLeave(int playerId) { }
            public void OnEnemySpawn(EnemyState enemy) { }
            public void OnEnemyDamaged(int enemyInstanceId, int hp, int attackerPlayerId, int damage) { }
            public void OnEnemyDead(int enemyInstanceId, int killerPlayerId, int[] droppedItemIds) { }
            public void OnSnapshot(RoomSnapshot snapshot) { }
            public void OnEnemyMove(EnemyMoveState[] enemies) { }
            public void OnDropSpawn(DropState[] drops) { }
            public void OnDropRemoved(int dropId, int playerId) { }
        }
    }
}
