using System;
using Terrace.Map;

namespace Terrace.Client.Core
{
    public enum ShopTradeKind
    {
        Buy,
        Sell,
    }

    /// <summary>店で買う・売るを 1 回試した結果。</summary>
    public readonly struct ShopTrade
    {
        public ShopTrade(ShopTradeKind kind, int itemId, int count, ShopResult result)
        {
            Kind = kind;
            ItemId = itemId;
            Count = count;
            Result = result;
        }

        public ShopTradeKind Kind { get; }
        public int ItemId { get; }
        public int Count { get; }

        /// <summary>結果(成功は ShopResult.Ok)。</summary>
        public ShopResult Result { get; }
    }

    /// <summary>
    /// NPC と店の係: NPC に話しかける、店を開く・閉じる、買う・売る。勘定そのものは ShopSession。
    /// 店を開いている間は移動の入力を受け付けない(GameSimulation が IsOpen を見る)。規則は docs/spec/economy-shop.md。
    /// </summary>
    public sealed class TradingSystem
    {
        private readonly GameContext _context;
        private readonly IItemCatalog? _items;
        private readonly IShopCatalog? _shops;

        public TradingSystem(GameContext context, IItemCatalog? items, IShopCatalog? shops)
        {
            _context = context;
            _items = items;
            _shops = shops;
        }

        /// <summary>開いている店。null なら開いていない。</summary>
        public ShopSession? ActiveShop { get; private set; }

        public bool IsOpen => ActiveShop != null;

        /// <summary>店ではない NPC に話しかけた。</summary>
        public event Action<Npc>? Talked;

        /// <summary>店の NPC だが、その店(ShopId)がまだ無かった。</summary>
        public event Action<Npc>? ShopNotFound;

        public event Action<ShopSession>? ShopOpened;

        /// <summary>店を閉じた。引数は閉じた店。</summary>
        public event Action<ShopSession>? ShopClosed;

        /// <summary>店で買う・売るを試した。</summary>
        public event Action<ShopTrade>? ShopTraded;

        /// <summary>NPC に話しかける。店なら開き、そうでなければ一言を表示する。店を開いたら true。</summary>
        public bool Interact(Npc npc)
        {
            if (npc.IsShop) return TryOpenShop(npc);

            Talked?.Invoke(npc);
            return false;
        }

        public bool TryOpenShop(Npc npc)
        {
            if (_context.Player.IsDead) return false;
            if (!npc.IsShop || _items == null || _shops == null) return false;

            var shop = _shops.Get(npc.ShopId);
            if (shop == null)
            {
                ShopNotFound?.Invoke(npc);
                return false;
            }

            ActiveShop = new ShopSession(npc, shop, _items, _context.Player);
            ShopOpened?.Invoke(ActiveShop);
            return true;
        }

        public void CloseShop()
        {
            var closed = ActiveShop;
            if (closed == null) return;
            ActiveShop = null;
            ShopClosed?.Invoke(closed);
        }

        /// <summary>店で買う。</summary>
        public ShopResult Buy(int itemId, int count = 1)
        {
            if (ActiveShop == null) return ShopResult.NotSoldHere;
            var result = ActiveShop.Buy(itemId, count);
            ShopTraded?.Invoke(new ShopTrade(ShopTradeKind.Buy, itemId, count, result));
            return result;
        }

        /// <summary>店で売る。</summary>
        public ShopResult Sell(int itemId, int count = 1)
        {
            if (ActiveShop == null) return ShopResult.NotInInventory;
            var result = ActiveShop.Sell(itemId, count);
            ShopTraded?.Invoke(new ShopTrade(ShopTradeKind.Sell, itemId, count, result));
            return result;
        }
    }
}
