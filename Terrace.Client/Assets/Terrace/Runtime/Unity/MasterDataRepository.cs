using System;
using System.IO;
using MessagePack;
using MessagePack.Resolvers;
using Newtonsoft.Json.Linq;
using Terrace.Client.Core;
using Terrace.MasterData;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// masterdata-build が出力した master.bytes(MasterMemory)を読み、ゲームが使う形(EnemyDefinition など)に変換する。
    /// MemoryDatabase / テーブル型は Terrace.MasterData(Shared から同期したテーブル定義 + Source Generator)が生成する。
    /// </summary>
    public sealed class MasterDataRepository
    {
        private readonly MemoryDatabase _database;

        public MasterDataRepository(byte[] databaseBinary, string? manifestJson = null)
        {
            // エディタ(Mono)では StandardResolver の動的生成で読める。IL2CPP では生成済みリゾルバの登録が別途必要
            var resolver = CompositeResolver.Create(MasterMemoryResolver.Instance, StandardResolver.Instance);
            _database = new MemoryDatabase(databaseBinary, formatterResolver: resolver);

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
                DropItemIds = enemy.DropItemIds ?? Array.Empty<int>(),
                DropRate = 0.5f,
            };
        }

        public string ItemName(int itemId)
            => _database.ItemTable.TryFindByItemId(itemId, out var item) ? item.Name : $"item{itemId}";
    }

    /// <summary>master.bytes が無いときの仮の敵定義(samples/csv/enemy.csv と同じ内容)。</summary>
    public static class FallbackEnemies
    {
        public static EnemyDefinition? Get(int enemyId)
        {
            switch (enemyId)
            {
                case 1: return new EnemyDefinition { EnemyId = 1, Name = "Slime", MaxHp = 10, Attack = 1, DropItemIds = new[] { 1, 4 } };
                case 2: return new EnemyDefinition { EnemyId = 2, Name = "Goblin", MaxHp = 30, Attack = 5, DropItemIds = new[] { 2, 3, 4 } };
                case 3: return new EnemyDefinition { EnemyId = 3, Name = "Ghost", MaxHp = 20, Attack = 3 };
                default: return null;
            }
        }
    }
}
