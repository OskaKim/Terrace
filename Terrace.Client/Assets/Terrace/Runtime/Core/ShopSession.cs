using System;
using System.Collections.Generic;
using Terrace.Map;

namespace Terrace.Client.Core
{
    public enum ShopResult
    {
        Ok,
        NotEnoughMeso,
        UnknownItem,
        NotSoldHere,
        NotInInventory,
    }

    /// <summary>店の商品 1 行。</summary>
    public sealed class ShopEntry
    {
        public ShopEntry(ItemInfo item, int price)
        {
            Item = item;
            Price = price;
        }

        public ItemInfo Item { get; }
        public int Price { get; }
    }

    /// <summary>持ち物の 1 行(同じアイテムはまとめる)。</summary>
    public sealed class InventoryEntry
    {
        public InventoryEntry(ItemInfo item, int count, int sellPrice)
        {
            Item = item;
            Count = count;
            SellPrice = sellPrice;
        }

        public ItemInfo Item { get; }
        public int Count { get; }
        public int SellPrice { get; }
    }

    /// <summary>
    /// NPC の店を開いている間の勘定。買う・売るでメソと持ち物を動かす。
    /// 売値は定価の半分(最低 1 メソ)。
    /// </summary>
    public sealed class ShopSession
    {
        private readonly IItemCatalog _items;

        public ShopSession(Npc npc, ShopDefinition shop, IItemCatalog items, PlayerState player)
        {
            Npc = npc;
            Shop = shop;
            _items = items;
            Player = player;

            var goods = new List<ShopEntry>();
            foreach (var itemId in shop.ItemIds)
            {
                var item = items.Get(itemId);
                if (item != null) goods.Add(new ShopEntry(item, item.Price));
            }
            Goods = goods;
        }

        public Npc Npc { get; }
        public ShopDefinition Shop { get; }
        public PlayerState Player { get; }
        public IReadOnlyList<ShopEntry> Goods { get; }

        /// <summary>買う・売るで状態が変わったとき。</summary>
        public event Action? Changed;

        public static int SellPriceOf(ItemInfo item) => Math.Max(1, item.Price / 2);

        public ShopEntry? FindGoods(int itemId)
        {
            foreach (var entry in Goods) if (entry.Item.ItemId == itemId) return entry;
            return null;
        }

        /// <summary>持ち物を分類ごとにまとめて返す。</summary>
        public IReadOnlyList<InventoryEntry> Inventory(ItemKind? kind = null)
        {
            var counts = new Dictionary<int, int>();
            var order = new List<int>();
            foreach (var itemId in Player.Inventory)
            {
                if (!counts.ContainsKey(itemId))
                {
                    counts[itemId] = 0;
                    order.Add(itemId);
                }
                counts[itemId]++;
            }

            var entries = new List<InventoryEntry>();
            foreach (var itemId in order)
            {
                var item = _items.Get(itemId) ?? new ItemInfo { ItemId = itemId, Name = $"item{itemId}", Price = 0 };
                if (kind.HasValue && item.Kind != kind.Value) continue;
                entries.Add(new InventoryEntry(item, counts[itemId], SellPriceOf(item)));
            }
            return entries;
        }

        public ShopResult Buy(int itemId, int count = 1)
        {
            if (count <= 0) return ShopResult.Ok;
            var entry = FindGoods(itemId);
            if (entry == null) return _items.Get(itemId) == null ? ShopResult.UnknownItem : ShopResult.NotSoldHere;

            var total = (long)entry.Price * count;
            if (Player.Meso < total) return ShopResult.NotEnoughMeso;

            Player.Meso -= total;
            for (var i = 0; i < count; i++) Player.Inventory.Add(itemId);
            Changed?.Invoke();
            return ShopResult.Ok;
        }

        public ShopResult Sell(int itemId, int count = 1)
        {
            if (count <= 0) return ShopResult.Ok;
            var item = _items.Get(itemId);
            if (item == null) return ShopResult.UnknownItem;

            var owned = 0;
            foreach (var id in Player.Inventory) if (id == itemId) owned++;
            if (owned < count) return ShopResult.NotInInventory;

            for (var i = 0; i < count; i++) Player.Inventory.Remove(itemId);
            Player.Meso += (long)SellPriceOf(item) * count;
            Changed?.Invoke();
            return ShopResult.Ok;
        }
    }
}
