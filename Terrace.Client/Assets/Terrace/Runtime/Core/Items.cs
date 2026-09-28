using System.Collections.Generic;

namespace Terrace.Client.Core
{
    /// <summary>店や持ち物で使う、アイテムの分類(タブ)。</summary>
    public enum ItemKind
    {
        Equip,
        Use,
        Etc,
    }

    /// <summary>アイテムの情報。マスタ(item.csv)から組み立てる。</summary>
    public sealed class ItemInfo
    {
        public int ItemId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int Price { get; set; }
        public ItemKind Kind { get; set; } = ItemKind.Etc;
        public string Description { get; set; } = string.Empty;

        public override string ToString() => $"{Name}#{ItemId} ({Kind}, {Price} メソ)";
    }

    /// <summary>アイテムの台帳。</summary>
    public interface IItemCatalog
    {
        ItemInfo? Get(int itemId);
        IReadOnlyList<ItemInfo> All { get; }
    }

    /// <summary>店の定義。ShopId ごとに売るアイテムの一覧。</summary>
    public sealed class ShopDefinition
    {
        public ShopDefinition(string shopId, string name, IReadOnlyList<int> itemIds)
        {
            ShopId = shopId;
            Name = name;
            ItemIds = itemIds;
        }

        public string ShopId { get; }
        public string Name { get; }
        public IReadOnlyList<int> ItemIds { get; }
    }

    public interface IShopCatalog
    {
        ShopDefinition? Get(string shopId);
    }

    /// <summary>
    /// 仮の店。"general" は全アイテム、"potion" は消費アイテムだけ、"equip" は装備だけを売る。
    /// 本来はマスタ(shop.csv)から組み立てる。
    /// </summary>
    public sealed class PlaceholderShopCatalog : IShopCatalog
    {
        private readonly IItemCatalog _items;

        public PlaceholderShopCatalog(IItemCatalog items)
        {
            _items = items;
        }

        public ShopDefinition? Get(string shopId)
        {
            switch (shopId)
            {
                case "general": return new ShopDefinition(shopId, "雑貨屋", Select(_ => true));
                case "potion": return new ShopDefinition(shopId, "薬屋", Select(item => item.Kind == ItemKind.Use));
                case "equip": return new ShopDefinition(shopId, "武具屋", Select(item => item.Kind == ItemKind.Equip));
                default: return null;
            }
        }

        private List<int> Select(System.Func<ItemInfo, bool> predicate)
        {
            var ids = new List<int>();
            foreach (var item in _items.All)
            {
                if (predicate(item)) ids.Add(item.ItemId);
            }
            return ids;
        }
    }
}
