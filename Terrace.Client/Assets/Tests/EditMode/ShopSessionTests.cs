using NUnit.Framework;
using Terrace.Client.Core;
using Terrace.Map;

namespace Terrace.Client.Tests.EditMode
{
    [TestFixture]
    public class ShopSessionTests
    {
        private static ShopSession NewSession(string shopId = "general", long meso = 1000)
        {
            var npc = new Npc { Id = 1, Name = "メリー", Kind = Npc.KindShop, ShopId = shopId };
            var shop = new PlaceholderShopCatalog(TestMaps.Catalog).Get(shopId)!;
            var player = new PlayerState(new PlayerProgression(LevelTable.Fallback)) { Meso = meso };
            return new ShopSession(npc, shop, TestMaps.Catalog, player);
        }

        [Test]
        public void 商品一覧は店の定義とアイテムの台帳から作る()
        {
            var general = NewSession("general");
            Assert.AreEqual(5, general.Goods.Count);
            Assert.AreEqual("Potion", general.Goods[0].Item.Name);
            Assert.AreEqual(50, general.Goods[0].Price);

            var potion = NewSession("potion");
            Assert.AreEqual(1, potion.Goods.Count, "薬屋は消費アイテムだけ");
            Assert.AreEqual(ItemKind.Use, potion.Goods[0].Item.Kind);

            var equip = NewSession("equip");
            Assert.AreEqual(2, equip.Goods.Count);
        }

        [Test]
        public void 買うとメソが減り持ち物に入る()
        {
            var session = NewSession(meso: 1000);
            var changed = 0;
            session.Changed += () => changed++;

            Assert.AreEqual(ShopResult.Ok, session.Buy(1));
            Assert.AreEqual(950, session.Player.Meso);
            Assert.AreEqual(1, session.Player.CountOf(1));

            Assert.AreEqual(ShopResult.Ok, session.Buy(2, 2));
            Assert.AreEqual(350, session.Player.Meso);
            Assert.AreEqual(2, session.Player.CountOf(2));
            Assert.AreEqual(2, changed);
        }

        [Test]
        public void メソが足りないと買えない()
        {
            var session = NewSession(meso: 40);

            Assert.AreEqual(ShopResult.NotEnoughMeso, session.Buy(1));
            Assert.AreEqual(40, session.Player.Meso);
            Assert.IsEmpty(session.Player.Inventory);
        }

        [Test]
        public void 店に無い物や知らない物は買えない()
        {
            var potion = NewSession("potion", meso: 10000);

            Assert.AreEqual(ShopResult.NotSoldHere, potion.Buy(2), "薬屋に剣は無い");
            Assert.AreEqual(ShopResult.UnknownItem, potion.Buy(999));
            Assert.AreEqual(10000, potion.Player.Meso);
        }

        [Test]
        public void 売ると半額でメソが増え持ち物から減る()
        {
            var session = NewSession(meso: 0);
            session.Player.Inventory.Add(2);
            session.Player.Inventory.Add(2);
            session.Player.Inventory.Add(5);

            Assert.AreEqual(ShopResult.Ok, session.Sell(2));
            Assert.AreEqual(150, session.Player.Meso);
            Assert.AreEqual(1, session.Player.CountOf(2));

            Assert.AreEqual(ShopResult.Ok, session.Sell(5), "定価 0 でも最低 1 メソ");
            Assert.AreEqual(151, session.Player.Meso);

            Assert.AreEqual(ShopResult.NotInInventory, session.Sell(2, 2), "1 個しか無い");
            Assert.AreEqual(ShopResult.NotInInventory, session.Sell(1));
            Assert.AreEqual(ShopResult.UnknownItem, session.Sell(999));
        }

        [Test]
        public void 持ち物は分類ごとにまとめて数える()
        {
            var session = NewSession();
            session.Player.Inventory.AddRange(new[] { 1, 2, 1, 4, 1, 3 });

            var all = session.Inventory();
            Assert.AreEqual(4, all.Count);
            Assert.AreEqual(("Potion", 3, 25), (all[0].Item.Name, all[0].Count, all[0].SellPrice));

            var equip = session.Inventory(ItemKind.Equip);
            Assert.AreEqual(2, equip.Count);
            Assert.AreEqual("Sword", equip[0].Item.Name);
            Assert.AreEqual(1, session.Inventory(ItemKind.Use).Count);
            Assert.AreEqual(1, session.Inventory(ItemKind.Etc).Count);
        }
    }
}
