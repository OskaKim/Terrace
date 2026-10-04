using System;
using System.Collections.Generic;
using NUnit.Framework;
using Terrace.Client.Core;
using Terrace.Map;

namespace Terrace.Client.Tests.EditMode
{
    /// <summary>係(~System)を GameSimulation を通さずに、GameContext と偽の世界の権威だけで試す。</summary>
    [TestFixture]
    public class GameSystemsTests
    {
        private static GameContext NewContext(MapData map, IWorldAuthority? authority = null)
        {
            var spawn = TravelSystem.ResolveSpawnPosition(map);
            var player = new PlayerState(new PlayerProgression(TestMaps.Levels())) { Meso = 1000 };
            var context = new GameContext(map, new CharacterMotor(map, MotorConfig.Default, spawn.X, spawn.Y), player, PlayerConfig.Default, new MessageLog(), TestMaps.ItemName);
            context.SetAuthority(authority ?? new FakeAuthority(map));
            return context;
        }

        [Test]
        public void KillRewardSystemは自分が倒した結果でだけ報酬を足す()
        {
            var map = TestMaps.Sample();
            var authority = new FakeAuthority(map);
            var context = NewContext(map, authority);
            var rewards = new KillRewardSystem(context);
            var killed = new List<EnemyEntity>();
            var leveled = new List<int>();
            var granted = new List<KillReward>();
            rewards.EnemyKilled += killed.Add;
            rewards.Rewarded += granted.Add;
            rewards.LeveledUp += leveled.Add;
            var slime = new EnemyEntity(1, TestMaps.Lookup(1)!, 25f, 5f);
            var goblin = new EnemyEntity(2, TestMaps.Lookup(2)!, 45f, 10f);

            authority.Kill(goblin, WorldActor.Other(9));
            Assert.AreEqual((0, 1000L, 0), (context.Player.Kills, context.Player.Meso, context.Player.Exp), "他の人が倒した");
            Assert.IsEmpty(killed);

            authority.Kill(slime, WorldActor.Self, 1, 4);
            Assert.AreEqual((1, 1010L), (context.Player.Kills, context.Player.Meso), "Slime は MaxHp と同じメソ");
            Assert.AreEqual((1, 3), (context.Player.Level, context.Player.Exp), "Slime の Exp");
            Assert.AreEqual(new[] { slime }, killed.ToArray());
            Assert.AreEqual((10, 3, 0), (granted[0].Meso, granted[0].Exp, granted[0].LevelsGained));
            Assert.AreEqual(new[] { 1, 4 }, granted[0].DroppedItemIds);
            Assert.IsEmpty(context.Messages.Items, "係は文言を作らない");

            authority.Kill(slime, WorldActor.Self);
            Assert.AreEqual(new[] { 2 }, leveled.ToArray(), "合わせて 6 でレベル 2");
        }

        [Test]
        public void TradingSystemは店を開いて買い売りし閉じられる()
        {
            var context = NewContext(TestMaps.Town());
            var trading = new TradingSystem(context, TestMaps.Catalog, new PlaceholderShopCatalog(TestMaps.Catalog));
            var trades = new List<ShopResult>();
            var closed = 0;
            trading.ShopTraded += trade => trades.Add(trade.Result);
            trading.ShopClosed += _ => closed++;

            Assert.IsTrue(trading.Interact(context.Map.FindNpc(1)!));
            Assert.IsTrue(trading.IsOpen);

            Assert.AreEqual(ShopResult.Ok, trading.Buy(1, 2));
            Assert.AreEqual(2, context.Player.CountOf(1));
            Assert.AreEqual(ShopResult.Ok, trading.Sell(1));
            Assert.AreEqual(1, context.Player.CountOf(1));
            Assert.AreEqual(new[] { ShopResult.Ok, ShopResult.Ok }, trades.ToArray());

            trading.CloseShop();
            Assert.IsFalse(trading.IsOpen);
            Assert.AreEqual(1, closed);
            Assert.AreEqual(ShopResult.NotSoldHere, trading.Buy(1), "閉じた後は買えない");
        }

        [Test]
        public void CombatSystemは一番近い敵への攻撃を世界の権威に頼むだけ()
        {
            var map = TestMaps.FlatWithPortals();
            var authority = new FakeAuthority(map);
            var context = NewContext(map, authority);
            var combat = new CombatSystem(context);
            var enemy = new EnemyEntity(1, TestMaps.Lookup(1)!, context.Motor.X + 1f, context.Motor.Y);
            authority.State.AddEnemy(enemy);
            AttackOutcome? last = null;
            combat.Attacked += outcome => last = outcome;

            combat.Attack();

            Assert.IsTrue(last.HasValue && last.Value.Hit);
            Assert.AreEqual((enemy, context.Player.Attack), authority.Attacks[0]);
            Assert.AreEqual(10, enemy.Hp, "HP を減らすのは権威");
        }

        [Test]
        public void GameNarratorは係のイベントから今と同じ文言を流す()
        {
            var map = TestMaps.Town();
            var authority = new FakeAuthority(map);
            var context = NewContext(map, authority);
            var life = new PlayerLifeSystem(context);
            var rewards = new KillRewardSystem(context);
            var looting = new LootingSystem(context);
            var travel = new TravelSystem(context, MotorConfig.Default, TestMaps.Lookup, new Random(1), null);
            var trading = new TradingSystem(context, TestMaps.Catalog, new PlaceholderShopCatalog(TestMaps.Catalog));
            var narrator = new GameNarrator(context, life, rewards, looting, travel, trading);

            trading.Interact(context.Map.FindNpc(3)!);
            Assert.IsTrue(context.Messages.Contains("案内人: 東の門から狩場へ行けるよ"));
            trading.Interact(context.Map.FindNpc(1)!);
            trading.Buy(1, 2);
            trading.Buy(3, 10);
            trading.CloseShop();
            authority.Kill(new EnemyEntity(1, TestMaps.Lookup(1)!, 0f, 0f), WorldActor.Self, 1, 4);
            authority.Kill(new EnemyEntity(2, TestMaps.Lookup(2)!, 0f, 0f), WorldActor.Other(9));
            life.Kill("落下");
            narrator.OnWentOffline("テスト");

            var texts = new List<string>();
            foreach (var item in context.Messages.Items) texts.Add(item.Text);
            Assert.AreEqual(new[]
            {
                "メリー: いらっしゃい、何でも揃うよ",
                "Potion を 2 個買った (残り 900 メソ)",
                "メソが足りない",
                "メリー: またどうぞ",
                "Slime を倒した (+10 メソ, +3 EXP) ドロップ: Potion, Herb",
                "player9 が Goblin を倒した",
                "倒れた… (落下)",
                "サーバーとの接続が切れた。オフラインで続けます (テスト)",
            }, texts.GetRange(texts.Count - 8, 8).ToArray());
        }

        /// <summary>頼まれたことを記録し、結果のイベントは試験から起こす世界の権威。</summary>
        private sealed class FakeAuthority : IWorldAuthority
        {
            public FakeAuthority(MapData map)
            {
                State = new WorldState(map);
            }

            public WorldState State { get; }
            public List<(EnemyEntity Target, int Damage)> Attacks { get; } = new List<(EnemyEntity, int)>();

            public event Action<EnemyDamage>? EnemyDamaged { add { } remove { } }
            public event Action<EnemyKill>? EnemyKilled;
            public event Action<EnemyEntity>? EnemySpawned { add { } remove { } }
            public event Action<ItemDrop>? DropSpawned { add { } remove { } }
            public event Action<DropRemoval>? DropRemoved { add { } remove { } }

            public void Tick(float dt) { }
            public void RequestAttack(EnemyEntity target, int damage) => Attacks.Add((target, damage));
            public void RequestPickup(ItemDrop drop) { }

            public void Kill(EnemyEntity enemy, WorldActor killer, params int[] droppedItemIds)
                => EnemyKilled?.Invoke(new EnemyKill(enemy, killer, droppedItemIds));
        }
    }
}
