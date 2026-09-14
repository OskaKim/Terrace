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

            Assert.AreEqual("Field 01", map.Name);
            Assert.AreEqual(9, map.Footholds.Count);
            Assert.AreEqual(4, map.Ladders.Count);
            Assert.AreEqual(2, map.Portals.Count);
            Assert.AreEqual(5, map.SpawnPoints.Count);
            Assert.AreEqual(2, map.FindFoothold(1)!.NextId);
            Assert.IsTrue(map.Ladders[0].IsRope);
            Assert.AreEqual(-5f, map.Bounds.Left);
            Assert.IsEmpty(map.Validate(), string.Join("\n", map.Validate()));
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
