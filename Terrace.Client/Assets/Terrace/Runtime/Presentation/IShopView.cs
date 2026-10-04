using System;
using System.Collections.Generic;
using Terrace.Client.Core;

namespace Terrace.Client.Presentation
{
    /// <summary>店の窓の 1 行(商品か持ち物)。</summary>
    public sealed class ShopRow
    {
        public ShopRow(int itemId, string itemName, string label, string priceText)
        {
            ItemId = itemId;
            ItemName = itemName;
            Label = label;
            PriceText = priceText;
        }

        public int ItemId { get; }

        /// <summary>品の名前(見た目の物の名前に使う)。</summary>
        public string ItemName { get; }

        /// <summary>行に出す名前(持ち物なら個数つき)。</summary>
        public string Label { get; }

        /// <summary>値段の文言(例「50 メソ」)。</summary>
        public string PriceText { get; }
    }

    /// <summary>
    /// ShopPresenter から見た店の窓。窓は言われた通りに描き、押されたことを知らせるだけ。
    /// 何を出すか・押されたら何をするかは ShopPresenter が決める。
    /// </summary>
    public interface IShopView
    {
        void Show();
        void Hide();

        /// <summary>題・NPC の名前・店の名前・NPC の絵の名前(無ければ null)。</summary>
        void SetHeader(string title, string npcName, string shopName, string? npcSprite);

        /// <summary>持っているメソ(文言)。</summary>
        void SetMeso(string meso);

        /// <summary>商品の行を作り直す。</summary>
        void SetGoods(IReadOnlyList<ShopRow> rows);

        /// <summary>持ち物の行を作り直す。</summary>
        void SetInventory(IReadOnlyList<ShopRow> rows);

        /// <summary>選んでいる行を目立たせる。選んでいなければ -1。</summary>
        void SetSelection(int goodsItemId, int inventoryItemId);

        /// <summary>持ち物のタブ。</summary>
        void SetInventoryTab(ItemKind tab);

        /// <summary>ワンクリック売却が入っているか。</summary>
        void SetOneClickSell(bool on);

        /// <summary>窓の下に出す操作の説明。</summary>
        void SetHint(string hint);

        /// <summary>商品の行が押された(ItemId)。</summary>
        event Action<int>? GoodsClicked;

        /// <summary>持ち物の行が押された(ItemId)。</summary>
        event Action<int>? InventoryClicked;

        /// <summary>持ち物のタブが押された。</summary>
        event Action<ItemKind>? TabClicked;

        event Action? BuyClicked;
        event Action? SellClicked;

        /// <summary>「店を出る」か「×」が押された。</summary>
        event Action? LeaveClicked;

        event Action? OneClickSellToggled;

        /// <summary>取り消しのキー(Esc)が押された。</summary>
        event Action? CancelPressed;
    }
}
