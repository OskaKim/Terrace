using System;
using System.Collections.Generic;
using System.IO;
using MessagePack;
using MessagePack.Resolvers;
using Newtonsoft.Json.Linq;
using Terrace.Client.Core;
using Terrace.MasterData;
using Terrace.MasterData.Tables;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// masterdata-build が出力した master.bytes(MasterMemory)を読み、ゲームが使う形(EnemyDefinition、LevelTable など)に変換する。
    /// MemoryDatabase / テーブル型は Terrace.MasterData(Shared から同期したテーブル定義 + Source Generator)が生成する。
    /// </summary>
    public sealed class MasterDataRepository : IItemCatalog
    {
        private readonly MemoryDatabase _database;
        private readonly List<ItemInfo> _items = new List<ItemInfo>();
        private readonly Dictionary<int, ItemInfo> _itemsById = new Dictionary<int, ItemInfo>();

        public MasterDataRepository(byte[] databaseBinary, string? manifestJson = null)
        {
            // エディタ(Mono)では StandardResolver の動的生成で読める。IL2CPP では生成済みリゾルバの登録が別途必要
            var resolver = CompositeResolver.Create(MasterMemoryResolver.Instance, StandardResolver.Instance);
            _database = new MemoryDatabase(databaseBinary, formatterResolver: resolver);

            foreach (var item in _database.ItemTable.All)
            {
                var info = new ItemInfo
                {
                    ItemId = item.ItemId,
                    Name = item.Name,
                    Price = item.Price,
                    Kind = KindOf(item.Category),
                    Description = $"{item.Category}",
                };
                _items.Add(info);
                _itemsById[item.ItemId] = info;
            }

            if (!string.IsNullOrEmpty(manifestJson))
            {
                try
                {
                    var manifest = JObject.Parse(manifestJson);
                    Sha256 = manifest.Value<string>("sha256") ?? string.Empty;
                    GeneratedAt = manifest.Value<string>("generatedAt") ?? string.Empty;
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"master.manifest.json を読めませんでした: {ex.Message}");
                }
            }
        }

        public string Sha256 { get; } = string.Empty;
        public string GeneratedAt { get; } = string.Empty;
        public string ShortVersion => Sha256.Length >= 8 ? Sha256.Substring(0, 8) : Sha256;
        public int ItemCount => _database.ItemTable.Count;
        public int EnemyCount => _database.EnemyTable.Count;
        public int QuestCount => _database.QuestTable.Count;
        public int LevelCount => _database.PlayerLevelTable.Count;

        public static string BinaryPath => Path.Combine(Application.streamingAssetsPath, "master.bytes");
        public static string ManifestPath => Path.Combine(Application.streamingAssetsPath, "master.manifest.json");

        public static MasterDataRepository LoadFromStreamingAssets()
        {
            if (!File.Exists(BinaryPath)) throw new FileNotFoundException($"master.bytes が見つかりません: {BinaryPath}", BinaryPath);
            var manifest = File.Exists(ManifestPath) ? File.ReadAllText(ManifestPath) : null;
            return new MasterDataRepository(File.ReadAllBytes(BinaryPath), manifest);
        }

        public EnemyDefinition? GetEnemy(int enemyId)
        {
            if (!_database.EnemyTable.TryFindByEnemyId(enemyId, out var enemy)) return null;
            return new EnemyDefinition
            {
                EnemyId = enemy.EnemyId,
                Name = enemy.Name,
                MaxHp = enemy.Hp,
                Attack = enemy.Attack,
                Exp = enemy.Exp,
                DropItemIds = enemy.DropItemIds ?? Array.Empty<int>(),
                DropRate = 0.5f,
            };
        }

        /// <summary>player_level テーブルをレベル表にする。行が 1 つも無ければ null(呼び手が LevelTable.Fallback を使う)。</summary>
        public LevelTable? GetLevelTable()
        {
            var rows = new List<LevelDefinition>();
            foreach (var row in _database.PlayerLevelTable.All)
            {
                rows.Add(new LevelDefinition { Level = row.Level, ExpToNext = row.ExpToNext, MaxHp = row.MaxHp, Attack = row.Attack });
            }
            return rows.Count == 0 ? null : new LevelTable(rows);
        }

        public string ItemName(int itemId)
            => _database.ItemTable.TryFindByItemId(itemId, out var item) ? item.Name : $"item{itemId}";

        // ---- IItemCatalog ----

        public ItemInfo? Get(int itemId) => _itemsById.TryGetValue(itemId, out var info) ? info : null;

        public IReadOnlyList<ItemInfo> All => _items;

        public static ItemKind KindOf(ItemCategory category)
        {
            switch (category)
            {
                case ItemCategory.Weapon:
                case ItemCategory.Armor:
                    return ItemKind.Equip;
                case ItemCategory.Consumable:
                    return ItemKind.Use;
                default:
                    return ItemKind.Etc;
            }
        }
    }

    /// <summary>master.bytes が無いときの仮のアイテム台帳(samples/csv/item.csv と同じ内容)。</summary>
    public sealed class FallbackItems : IItemCatalog
    {
        private readonly List<ItemInfo> _items = new List<ItemInfo>
        {
            new ItemInfo { ItemId = 1, Name = "Potion", Price = 50, Kind = ItemKind.Use },
            new ItemInfo { ItemId = 2, Name = "Sword", Price = 300, Kind = ItemKind.Equip },
            new ItemInfo { ItemId = 3, Name = "Shield, Large", Price = 500, Kind = ItemKind.Equip },
            new ItemInfo { ItemId = 4, Name = "Herb", Price = 10, Kind = ItemKind.Etc },
            new ItemInfo { ItemId = 5, Name = "Ore", Price = 0, Kind = ItemKind.Etc },
        };

        public ItemInfo? Get(int itemId)
        {
            foreach (var item in _items) if (item.ItemId == itemId) return item;
            return null;
        }

        public IReadOnlyList<ItemInfo> All => _items;
    }

    /// <summary>master.bytes が無いときの仮の敵定義(samples/csv/enemy.csv と同じ内容)。レベル表の逃げ道は Core の LevelTable.Fallback。</summary>
    public static class FallbackEnemies
    {
        public static EnemyDefinition? Get(int enemyId)
        {
            switch (enemyId)
            {
                case 1: return new EnemyDefinition { EnemyId = 1, Name = "Slime", MaxHp = 10, Attack = 1, Exp = 3, DropItemIds = new[] { 1, 4 } };
                case 2: return new EnemyDefinition { EnemyId = 2, Name = "Goblin", MaxHp = 30, Attack = 5, Exp = 10, DropItemIds = new[] { 2, 3, 4 } };
                case 3: return new EnemyDefinition { EnemyId = 3, Name = "Ghost", MaxHp = 20, Attack = 3, Exp = 6 };
                default: return null;
            }
        }
    }
}
