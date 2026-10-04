using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Terrace.Client.Unity;
using UnityEngine;

namespace Terrace.Client.Tests.EditMode
{
    /// <summary>Resources に置いた効果音と BGM が AudioClip として読めること、無い音は黙って null になることを確かめる。</summary>
    [TestFixture]
    public class AudioAssetsTests
    {
        [Test]
        public void 対応表にある効果音はすべてResourcesから読め読み込み時に展開される()
        {
            var audio = AudioLibrary.Load();

            foreach (var effect in AudioLibrary.AllSoundEffects)
            {
                var clip = audio.Get(effect);
                Assert.IsNotNull(clip, $"{effect} → Audio/Se/{AudioLibrary.FileName(effect)}");
                Assert.Greater(clip!.length, 0f, effect.ToString());
                Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, clip.loadType, $"{effect} は AudioImportProcessor の設定で取り込まれている");
                Assert.IsTrue(clip.preloadAudioData, $"{effect} は先に読み込む設定");
            }
            Assert.AreEqual(AudioLibrary.AllSoundEffects.Count, audio.Preload());
        }

        [Test]
        public void ファイルの無い音はnullを返し例外にならない()
        {
            var audio = AudioLibrary.Load("Terrace/Audio/__missing__/");

            foreach (var effect in AudioLibrary.AllSoundEffects)
            {
                Assert.IsNull(audio.Get(effect), effect.ToString());
            }
            Assert.AreEqual(0, audio.Preload());

            var bgm = MapRegistry.LoadFromStreamingAssets().Get(100)!.Bgm;
            Assert.IsNull(audio.GetBgm(bgm), "BGM も無い");
            Assert.IsNull(AudioLibrary.Load().GetBgm("__missing__"));
            Assert.IsNull(AudioLibrary.Load().GetBgm(string.Empty), "空は無音");
        }

        [Test]
        public void mapsフォルダの全マップのBGMはResourcesから読めストリーミングで取り込まれる()
        {
            var audio = AudioLibrary.Load();

            foreach (var path in Directory.GetFiles(MapLoader.MapsDirectory, "*.json"))
            {
                var map = MapLoader.LoadFromStreamingAssets(Path.GetFileName(path));
                if (string.IsNullOrEmpty(map.Bgm)) continue;
                var clip = audio.GetBgm(map.Bgm);
                Assert.IsNotNull(clip, $"{Path.GetFileName(path)} → Audio/Bgm/{map.Bgm}");
                Assert.Greater(clip!.length, 0f, map.Bgm);
                Assert.AreEqual(AudioClipLoadType.Streaming, clip.loadType, $"{map.Bgm} は AudioImportProcessor の設定で取り込まれている");
            }
        }

        [Test]
        public void 町と狩場はそれぞれ違う曲を持つ()
        {
            var registry = MapRegistry.LoadFromStreamingAssets();
            var seen = new HashSet<string>();

            foreach (var map in registry.Maps.Values)
            {
                Assert.IsNotEmpty(map.Bgm, $"{map.Name} に bgm がある");
                Assert.IsTrue(seen.Add(map.Bgm), $"{map.Name} の {map.Bgm} は他のマップと重なっている");
            }
        }
    }
}
