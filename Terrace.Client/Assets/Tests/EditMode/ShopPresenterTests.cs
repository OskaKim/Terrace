using System;
using System.Collections.Generic;
using NUnit.Framework;
using Terrace.Client.Core;
using Terrace.Client.Presentation;

namespace Terrace.Client.Tests.EditMode
{
    /// <summary>店の窓の Presenter を、Unity の窓の代わりに記録するだけの偽の窓で試す。</summary>
    [TestFixture]
    public class ShopPresenterTests
    {
        private const int Potion = 1;
        private const int Sword = 2;

        private static (GameSimulation Sim, FakeShopView View, ShopPresenter Presenter) OpenShop()
        {
            var sim = TestMaps.NewWorldSimulation(TestMaps.Town());
            var view = new FakeShopView();
            var presenter = new ShopPresenter(sim, view);
            sim.Trading.Interact(sim.Map.FindNpc(1)!);
            return (sim, view, presenter);
        }

        [Test]
        public void 開くと窓が出て商品の行と持ち物のタブが描かれ閉じると隠れる()
        {
            var (sim, view, presenter) = OpenShop();

            Assert.IsTrue(view.Visible);
            Assert.IsTrue(presenter.IsOpen);
            Assert.AreEqual("メリー の店", view.Title);
            Assert.AreEqual(5, view.Goods.Count, "雑貨屋は 5 品");
            Assert.AreEqual(("Potion", "50 メソ"), (view.Goods[0].Label, view.Goods[0].PriceText));
            Assert.AreEqual(ItemKind.Use, view.Tab);
            Assert.AreEqual("3,000", view.Meso);

            sim.Trading.CloseShop();
            Assert.IsFalse(view.Visible);
            Assert.IsFalse(presenter.IsOpen);
        }

        [Test]
        public void 行を選んで買うと買え持ち物の行と所持金が描き直される()
        {
            var (sim, view, _) = OpenShop();

            view.ClickGoods(Potion);
            Assert.AreEqual((Potion, -1), view.Selection);
            view.ClickBuy();

            Assert.AreEqual(1, sim.Player.CountOf(Potion));
            Assert.AreEqual("2,950", view.Meso);
            Assert.AreEqual("Potion  x1", view.Inventory[0].Label);
            Assert.AreEqual("25 メソ", view.Inventory[0].PriceText, "売値は定価の半分");
        }

        [Test]
        public void 何も選ばずに買う売るを押すとメッセージ欄で知らせる()
        {
            var (sim, view, _) = OpenShop();

            view.ClickBuy();
            view.ClickSell();

            Assert.IsTrue(sim.Messages.Contains("買う商品をクリックして選んでください"));
            Assert.IsTrue(sim.Messages.Contains("売る持ち物をクリックして選んでください"));
            Assert.AreEqual(3000, sim.Player.Meso);
        }

        [Test]
        public void 売って0個になると持ち物の選択が外れる()
        {
            var (sim, view, presenter) = OpenShop();
            view.ClickGoods(Potion);
            view.ClickBuy();
            view.ClickBuy();

            view.ClickInventory(Potion);
            view.ClickSell();
            Assert.AreEqual(Potion, presenter.SelectedInventoryItemId, "まだ 1 個ある");

            view.ClickSell();
            Assert.AreEqual(0, sim.Player.CountOf(Potion));
            Assert.AreEqual(-1, presenter.SelectedInventoryItemId);
            Assert.AreEqual(-1, view.Selection.Inventory);
        }

        [Test]
        public void ワンクリック売却なら持ち物を押すだけで売れる()
        {
            var (sim, view, presenter) = OpenShop();
            view.ClickGoods(Potion);
            view.ClickBuy();

            view.ToggleOneClick();
            Assert.IsTrue(presenter.OneClickSell);
            Assert.IsTrue(view.OneClick);
            StringAssert.Contains("即売却", view.Hint);

            view.ClickInventory(Potion);
            Assert.AreEqual(0, sim.Player.CountOf(Potion));
        }

        [Test]
        public void タブを替えると持ち物の選択が外れその分類の行だけが出る()
        {
            var (sim, view, presenter) = OpenShop();
            view.ClickGoods(Potion);
            view.ClickBuy();
            view.ClickGoods(Sword);
            view.ClickBuy();
            view.ClickInventory(Potion);

            view.ClickTab(ItemKind.Equip);

            Assert.AreEqual(ItemKind.Equip, view.Tab);
            Assert.AreEqual(-1, presenter.SelectedInventoryItemId);
            Assert.AreEqual(new[] { "Sword  x1" }, Labels(view.Inventory));
        }

        [Test]
        public void Escで店を出る()
        {
            var (sim, view, _) = OpenShop();

            view.PressCancel();

            Assert.IsFalse(sim.Trading.IsOpen);
            Assert.IsFalse(view.Visible);
            Assert.IsTrue(sim.Messages.Contains("メリー: またどうぞ"));
        }

        private static string[] Labels(IReadOnlyList<ShopRow> rows)
        {
            var labels = new string[rows.Count];
            for (var i = 0; i < rows.Count; i++) labels[i] = rows[i].Label;
            return labels;
        }

        /// <summary>言われたことを覚えておき、押されたことは試験から起こす偽の窓。</summary>
        private sealed class FakeShopView : IShopView
        {
            public bool Visible { get; private set; }
            public string Title { get; private set; } = string.Empty;
            public string Meso { get; private set; } = string.Empty;
            public string Hint { get; private set; } = string.Empty;
            public IReadOnlyList<ShopRow> Goods { get; private set; } = Array.Empty<ShopRow>();
            public IReadOnlyList<ShopRow> Inventory { get; private set; } = Array.Empty<ShopRow>();
            public (int Goods, int Inventory) Selection { get; private set; } = (-1, -1);
            public ItemKind Tab { get; private set; }
            public bool OneClick { get; private set; }

            public event Action<int>? GoodsClicked;
            public event Action<int>? InventoryClicked;
            public event Action<ItemKind>? TabClicked;
            public event Action? BuyClicked;
            public event Action? SellClicked;
            public event Action? LeaveClicked;
            public event Action? OneClickSellToggled;
            public event Action? CancelPressed;

            public void Show() => Visible = true;
            public void Hide() => Visible = false;
            public void SetHeader(string title, string npcName, string shopName, string? npcSprite) => Title = title;
            public void SetMeso(string meso) => Meso = meso;
            public void SetGoods(IReadOnlyList<ShopRow> rows) => Goods = rows;
            public void SetInventory(IReadOnlyList<ShopRow> rows) => Inventory = rows;
            public void SetSelection(int goodsItemId, int inventoryItemId) => Selection = (goodsItemId, inventoryItemId);
            public void SetInventoryTab(ItemKind tab) => Tab = tab;
            public void SetOneClickSell(bool on) => OneClick = on;
            public void SetHint(string hint) => Hint = hint;

            public void ClickGoods(int itemId) => GoodsClicked?.Invoke(itemId);
            public void ClickInventory(int itemId) => InventoryClicked?.Invoke(itemId);
            public void ClickTab(ItemKind kind) => TabClicked?.Invoke(kind);
            public void ClickBuy() => BuyClicked?.Invoke();
            public void ClickSell() => SellClicked?.Invoke();
            public void ClickLeave() => LeaveClicked?.Invoke();
            public void ToggleOneClick() => OneClickSellToggled?.Invoke();
            public void PressCancel() => CancelPressed?.Invoke();
        }
    }
}
