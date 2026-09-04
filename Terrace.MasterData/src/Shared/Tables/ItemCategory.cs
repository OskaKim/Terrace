namespace Terrace.MasterData.Tables
{
    /// <summary>アイテムの分類。CSV では名前(例: Weapon)または数値で指定する。</summary>
    public enum ItemCategory
    {
        None = 0,
        Weapon = 1,
        Armor = 2,
        Consumable = 3,
        Material = 4,
    }
}
