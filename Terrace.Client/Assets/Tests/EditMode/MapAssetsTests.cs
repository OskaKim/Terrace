using System.IO;
using NUnit.Framework;
using Terrace.Client.Unity;

namespace Terrace.Client.Tests.EditMode
{
    /// <summary>StreamingAssets に置いた実データ(マップ JSON と master.bytes)が読めて整合していることを確かめる。</summary>
    [TestFixture]
    public class MapAssetsTests
    {
        [Test]
        public void field01はNewtonsoftで読めて検証を通る()
        {
            var map = MapLoader.LoadFromStreamingAssets("field01.json");

            Assert.AreEqual("草原", map.Name);
            Assert.AreEqual(9, map.Footholds.Count);
            Assert.AreEqual(4, map.Ladders.Count);
            Assert.AreEqual(2, map.Portals.Count);
            Assert.AreEqual(5, map.SpawnPoints.Count);
            Assert.AreEqual(8, map.Decorations.Count);
            Assert.AreEqual(2, map.FindFoothold(1)!.NextId);
            Assert.IsTrue(map.Ladders[0].IsRope);
            Assert.AreEqual(-5f, map.Bounds.Left);
            Assert.AreEqual(Terrace.Map.PortalKind.Spawn, map.FindPortalByName("spawn")!.Kind, "kind は文字列から読める");
            Assert.AreEqual(100, map.FindPortalByName("east_gate")!.TargetMapId);
            Assert.IsEmpty(map.Validate(), string.Join("\n", map.Validate()));
        }

        [Test]
        public void 町には店のNPCと家の飾りがあり門が両側の狩場へ繋がる()
        {
            var town = MapLoader.LoadFromStreamingAssets("town01.json");

            Assert.AreEqual(100, town.Id);
            Assert.AreEqual(3, town.Npcs.Count);
            Assert.AreEqual(2, town.Npcs.FindAll(n => n.IsShop).Count);
            Assert.AreEqual("general", town.FindNpc(1)!.ShopId);
            Assert.Greater(town.Decorations.Count, 40);
            Assert.IsEmpty(town.SpawnPoints, "町に敵はいない");
            Assert.AreEqual(1, town.FindPortalByName("west_gate")!.TargetMapId);
            Assert.AreEqual(2, town.FindPortalByName("east_gate")!.TargetMapId);
            Assert.IsNotNull(town.FindSpawnPortal());
            Assert.IsEmpty(town.Validate(), string.Join("\n", town.Validate()));
        }

        [Test]
        public void 地図一覧は町と狩場2枚を読みサンプルは除く()
        {
            var registry = MapRegistry.LoadFromStreamingAssets();

            Assert.AreEqual(3, registry.Count, string.Join("\n", registry.Warnings));
            Assert.IsEmpty(registry.Warnings);
            Assert.AreEqual("草原", registry.Get(1)!.Name);
            Assert.AreEqual("砂の崖", registry.Get(2)!.Name);
            Assert.AreEqual("テラスの町", registry.Get(100)!.Name);
            Assert.AreEqual("field02.json", registry.FileOf(2));
            Assert.IsNull(registry.Get(999));

            // 門は互いに向き合っている
            foreach (var map in registry.Maps.Values)
            {
                foreach (var portal in map.Portals)
                {
                    if (portal.IsSpawn) continue;
                    var target = registry.Get(portal.TargetMapId);
                    Assert.IsNotNull(target, $"{map.Name} の {portal.Name} の行き先 map {portal.TargetMapId}");
                    Assert.IsNotNull(target!.FindPortalByName(portal.TargetPortalName), $"{map.Name} の {portal.Name} の行き先 '{portal.TargetPortalName}' が {target.Name} に無い");
                }
            }
        }

        [Test]
        public void 飾りの絵はすべて素材にある()
        {
            var art = ArtLibrary.Load();
            var registry = MapRegistry.LoadFromStreamingAssets();

            foreach (var map in registry.Maps.Values)
            {
                foreach (var decoration in map.Decorations)
                {
                    Assert.IsNotNull(art.Get(decoration.Sprite), $"{map.Name}: {decoration.Sprite}");
                }
                foreach (var npc in map.Npcs)
                {
                    Assert.IsNotNull(art.NpcSprite(npc.Sprite), $"{map.Name}: NPC {npc.Name} の絵 {npc.Sprite}");
                }
            }
        }

        [Test]
        public void サンプルマップも読めて検証を通る()
        {
            var map = MapLoader.LoadFromStreamingAssets("sample_map.json");

            Assert.AreEqual(6, map.Footholds.Count);
            Assert.IsEmpty(map.Validate());
        }

        [Test]
        public void mapsフォルダの全マップは検証を通る()
        {
            foreach (var path in Directory.GetFiles(MapLoader.MapsDirectory, "*.json"))
            {
                var map = MapLoader.LoadFromStreamingAssets(Path.GetFileName(path));
                Assert.IsEmpty(map.Validate(), $"{Path.GetFileName(path)}: {string.Join("\n", map.Validate())}");
            }
        }

        [Test]
        public void masterBytesをMasterMemoryで読める()
        {
            var master = MasterDataRepository.LoadFromStreamingAssets();

            Assert.AreEqual(5, master.ItemCount);
            Assert.AreEqual(3, master.EnemyCount);
            Assert.AreEqual(3, master.QuestCount);

            var slime = master.GetEnemy(1)!;
            Assert.AreEqual("Slime", slime.Name);
            Assert.AreEqual(10, slime.MaxHp);
            Assert.AreEqual(1, slime.Attack);
            Assert.AreEqual(new[] { 1, 4 }, slime.DropItemIds);

            Assert.AreEqual("Potion", master.ItemName(1));
            Assert.AreEqual("Shield, Large", master.ItemName(3));
            Assert.AreEqual("item999", master.ItemName(999));
            Assert.IsNull(master.GetEnemy(999));
            Assert.AreEqual(8, master.ShortVersion.Length);
        }
    }
}
