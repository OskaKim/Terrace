using NUnit.Framework;
using Terrace.Client.Unity;

namespace Terrace.Client.Tests.EditMode
{
    /// <summary>Resources に置いた Kenney の素材が Sprite として読めて、必要な絵が揃っていることを確かめる。</summary>
    [TestFixture]
    public class ArtAssetsTests
    {
        [Test]
        public void 素材が揃っていて主人公の絵は足元中央が原点()
        {
            var art = ArtLibrary.Load();

            Assert.IsTrue(art.IsAvailable);
            var player = art.Player!;
            Assert.AreEqual(11, player.Walk.Length, "p1_walk01〜11");
            Assert.AreEqual(2, player.Climb.Length);
            Assert.AreNotSame(player.Stand, player.Jump);
            Assert.AreNotSame(player.Stand, player.Duck);
            Assert.AreEqual(1.31f, player.Height, 0.05f, "92px / 70ppu");
            Assert.AreEqual(0f, player.Stand.bounds.min.y, 0.01f, "足元が原点");
            Assert.AreEqual(0f, player.Stand.bounds.center.x, 0.01f, "左右中央が原点");
        }

        [Test]
        public void 敵の絵はSlimeとGoblinとGhostに割り当たっている()
        {
            var art = ArtLibrary.Load();

            for (var enemyId = 1; enemyId <= 3; enemyId++)
            {
                var enemy = art.GetEnemy(enemyId);
                Assert.IsNotNull(enemy, $"enemy {enemyId}");
                Assert.AreEqual(2, enemy!.Walk.Length, $"enemy {enemyId} walk frames");
                Assert.AreNotSame(enemy.Idle, enemy.Dead);
                Assert.Greater(enemy.Width, 0.5f);
                Assert.Less(enemy.Width, 1.2f);
                Assert.Greater(enemy.Height, 0.3f);
            }
            Assert.IsNotNull(art.GetEnemy(99), "未知の敵にも代役がある");
            Assert.Less(art.GetEnemy(1)!.Height, art.GetEnemy(3)!.Height, "Slime は Ghost より低い");
        }

        [Test]
        public void タイルと品と背景とHUDの絵がある()
        {
            var art = ArtLibrary.Load();

            foreach (var name in new[] { "grassMid", "grassCenter", "ladder_mid", "ropeVertical", "door_openMid", "door_openTop" })
            {
                Assert.IsNotNull(art.Tile(name), name);
            }
            Assert.AreEqual(1f, art.Tile("grassMid")!.bounds.size.x, 0.01f, "タイル 1 枚 = 1 unit");

            for (var itemId = 1; itemId <= 5; itemId++) Assert.IsNotNull(art.Item(itemId), $"item {itemId}");
            Assert.AreEqual("gemRed", ArtLibrary.ItemSpriteName(1));
            Assert.AreEqual("star", ArtLibrary.ItemSpriteName(999));
            Assert.IsNotNull(art.Item(999));

            Assert.IsNotNull(art.Background);
            foreach (var name in new[] { "hud_heartFull", "hud_heartHalf", "hud_heartEmpty", "hud_p1" })
            {
                Assert.IsNotNull(art.Hud(name), name);
            }
        }
    }
}
