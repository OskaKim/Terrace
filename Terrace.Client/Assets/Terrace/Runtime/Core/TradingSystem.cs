using System;
using Terrace.Map;

namespace Terrace.Client.Core
{
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

        public event Action<ShopSession>? ShopOpened;
        public event Action? ShopClosed;

        /// <summary>店で買う・売るを試した。引数はその結果(成功は ShopResult.Ok)。</summary>
        public event Action<ShopResult>? ShopTraded;

        /// <summary>NPC に話しかける。店なら開き、そうでなければ一言を表示する。店を開いたら true。</summary>
        public bool Interact(Npc npc)
        {
            if (npc.IsShop) return TryOpenShop(npc);

            _context.Messages.Add(_context.Time, string.IsNullOrEmpty(npc.Greeting) ? $"{npc.Name}: ……" : $"{npc.Name}: {npc.Greeting}");
            return false;
        }

        public bool TryOpenShop(Npc npc)
        {
            if (_context.Player.IsDead) return false;
            if (!npc.IsShop || _items == null || _shops == null) return false;

            var shop = _shops.Get(npc.ShopId);
            if (shop == null)
            {
                _context.Messages.Add(_context.Time, $"{npc.Name}: 店 '{npc.ShopId}' はまだありません");
                return false;
            }

            ActiveShop = new ShopSession(npc, shop, _items, _context.Player);
            _context.Messages.Add(_context.Time, string.IsNullOrEmpty(npc.Greeting) ? $"{npc.Name}: いらっしゃい" : $"{npc.Name}: {npc.Greeting}");
            ShopOpened?.Invoke(ActiveShop);
            return true;
        }

        public void CloseShop()
        {
            if (ActiveShop == null) return;
            _context.Messages.Add(_context.Time, $"{ActiveShop.Npc.Name}: またどうぞ");
            ActiveShop = null;
            ShopClosed?.Invoke();
        }

        /// <summary>店で買う。結果をメッセージにも流す。</summary>
        public ShopResult Buy(int itemId, int count = 1)
        {
            if (ActiveShop == null) return ShopResult.NotSoldHere;
            var result = ActiveShop.Buy(itemId, count);
            switch (result)
            {
                case ShopResult.Ok:
                    _context.Messages.Add(_context.Time, $"{_context.ItemName(itemId)} を {count} 個買った (残り {_context.Player.Meso:N0} メソ)");
                    break;
                case ShopResult.NotEnoughMeso:
                    _context.Messages.Add(_context.Time, "メソが足りない");
                    break;
                default:
                    _context.Messages.Add(_context.Time, "それは買えない");
                    break;
            }
            ShopTraded?.Invoke(result);
            return result;
        }

        /// <summary>店で売る。</summary>
        public ShopResult Sell(int itemId, int count = 1)
        {
            if (ActiveShop == null) return ShopResult.NotInInventory;
            var result = ActiveShop.Sell(itemId, count);
            _context.Messages.Add(_context.Time, result == ShopResult.Ok
                ? $"{_context.ItemName(itemId)} を {count} 個売った (所持 {_context.Player.Meso:N0} メソ)"
                : "それは売れない");
            ShopTraded?.Invoke(result);
            return result;
        }
    }
}
