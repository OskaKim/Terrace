using System.Collections.Generic;
using Terrace.Client.Core;

namespace Terrace.Client.Presentation
{
    /// <summary>
    /// 店の窓の Presenter。TradingSystem が店を開いたら窓を出し、閉じたら隠す。
    /// 窓に出す文言と行を組み立て、窓だけの状態(選んだ商品・選んだ持ち物・持ち物のタブ・ワンクリック売却)を持ち、
    /// 押された知らせを受けて TradingSystem を操作する。勘定そのものは ShopSession。
    ///
    /// 窓の決まり: 行をクリックで選ぶ。タブを替えると持ち物の選択を外す。売って 0 個になったら持ち物の選択を外す。
    /// ワンクリック売却が入っていれば、持ち物をクリックするだけで売る。何も選ばずに買う・売るを押したら、メッセージ欄で知らせる。
    /// </summary>
    public sealed class ShopPresenter
    {
        private readonly GameSimulation _simulation;
        private readonly IShopView _view;
        private ShopSession? _session;

        public ShopPresenter(GameSimulation simulation, IShopView view)
        {
            _simulation = simulation;
            _view = view;

            simulation.Trading.ShopOpened += Open;
            simulation.Trading.ShopClosed += Close;
            view.GoodsClicked += SelectGoods;
            view.InventoryClicked += SelectInventory;
            view.TabClicked += SelectInventoryTab;
            view.BuyClicked += Buy;
            view.SellClicked += Sell;
            view.LeaveClicked += Leave;
            view.OneClickSellToggled += ToggleOneClickSell;
            view.CancelPressed += Leave;

            view.Hide();
            if (simulation.Trading.ActiveShop != null) Open(simulation.Trading.ActiveShop);
        }

        public bool IsOpen => _session != null;
        public ShopSession? Session => _session;
        public int SelectedGoodsItemId { get; private set; } = -1;
        public int SelectedInventoryItemId { get; private set; } = -1;
        public ItemKind InventoryTab { get; private set; } = ItemKind.Use;
        public bool OneClickSell { get; private set; }

        /// <summary>購読をやめる(窓を壊すとき)。</summary>
        public void Dispose()
        {
            _simulation.Trading.ShopOpened -= Open;
            _simulation.Trading.ShopClosed -= Close;
            if (_session != null) _session.Changed -= Refresh;
            _session = null;
        }

        // ---- 押された ----

        public void SelectGoods(int itemId)
        {
            SelectedGoodsItemId = itemId;
            _view.SetSelection(SelectedGoodsItemId, SelectedInventoryItemId);
        }

        public void SelectInventory(int itemId)
        {
            SelectedInventoryItemId = itemId;
            _view.SetSelection(SelectedGoodsItemId, SelectedInventoryItemId);
            if (OneClickSell) _simulation.Trading.Sell(itemId);
        }

        public void SelectInventoryTab(ItemKind kind)
        {
            InventoryTab = kind;
            SelectedInventoryItemId = -1;
            Refresh();
        }

        public void Buy()
        {
            if (_session == null) return;
            if (SelectedGoodsItemId < 0)
            {
                Notify("買う商品をクリックして選んでください");
                return;
            }
            _simulation.Trading.Buy(SelectedGoodsItemId);
        }

        public void Sell()
        {
            if (_session == null) return;
            if (SelectedInventoryItemId < 0)
            {
                Notify("売る持ち物をクリックして選んでください");
                return;
            }
            _simulation.Trading.Sell(SelectedInventoryItemId);
            if (_session != null && _session.Player.CountOf(SelectedInventoryItemId) == 0)
            {
                SelectedInventoryItemId = -1;
                _view.SetSelection(SelectedGoodsItemId, SelectedInventoryItemId);
            }
        }

        /// <summary>店を出る(「店を出る」「×」・Esc)。開いていなければ何もしない。</summary>
        public void Leave()
        {
            if (_session == null) return;
            _simulation.Trading.CloseShop();
        }

        public void ToggleOneClickSell()
        {
            OneClickSell = !OneClickSell;
            Refresh();
        }

        // ---- 開閉と描き直し ----

        private void Open(ShopSession session)
        {
            if (_session != null) _session.Changed -= Refresh;
            _session = session;
            _session.Changed += Refresh;
            SelectedGoodsItemId = -1;
            SelectedInventoryItemId = -1;
            _view.Show();
            Refresh();
        }

        private void Close(ShopSession closed)
        {
            if (_session != null) _session.Changed -= Refresh;
            _session = null;
            _view.Hide();
        }

        /// <summary>今の店と窓の状態で、窓をすべて描き直す。</summary>
        public void Refresh()
        {
            var session = _session;
            if (session == null) return;
            var npc = session.Npc;
            _view.SetHeader($"{npc.Name} の店", npc.Name, session.Shop.Name, string.IsNullOrEmpty(npc.Sprite) ? null : npc.Sprite);
            _view.SetMeso($"{session.Player.Meso:N0}");
            _view.SetOneClickSell(OneClickSell);
            _view.SetInventoryTab(InventoryTab);

            var goods = new List<ShopRow>();
            foreach (var entry in session.Goods)
            {
                goods.Add(new ShopRow(entry.Item.ItemId, entry.Item.Name, entry.Item.Name, $"{entry.Price:N0} メソ"));
            }
            _view.SetGoods(goods);

            var inventory = new List<ShopRow>();
            foreach (var entry in session.Inventory(InventoryTab))
            {
                inventory.Add(new ShopRow(entry.Item.ItemId, entry.Item.Name, $"{entry.Item.Name}  x{entry.Count}", $"{entry.SellPrice:N0} メソ"));
            }
            _view.SetInventory(inventory);

            _view.SetSelection(SelectedGoodsItemId, SelectedInventoryItemId);
            _view.SetHint(OneClickSell
                ? "持ち物をクリックすると即売却。商品をクリックして選び「買う」。Esc で閉じる"
                : "行をクリックして選び、「買う」「売る」を押す。Esc で閉じる");
        }

        /// <summary>窓の操作についての知らせをメッセージ欄に出す。</summary>
        private void Notify(string text) => _simulation.Messages.Add(_simulation.Time, text);
    }
}
