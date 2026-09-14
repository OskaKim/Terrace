using MasterMemory;
using MessagePack;

namespace Terrace.MasterData.Tables
{
    /// <summary>アイテムマスタ。対応する CSV は item.csv。</summary>
    [MemoryTable("item"), MessagePackObject(true)]
    public sealed class Item
    {
        [PrimaryKey]
        public int ItemId { get; set; }

        public string Name { get; set; } = string.Empty;

        public int Price { get; set; }

        public ItemCategory Category { get; set; }
    }
}
