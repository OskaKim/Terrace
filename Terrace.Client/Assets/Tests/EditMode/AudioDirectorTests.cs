using System.Collections.Generic;
using NUnit.Framework;
using Terrace.Client.Core;
using Terrace.Client.Unity;
using Terrace.Map;
using UnityEngine;

namespace Terrace.Client.Tests.EditMode
{
    /// <summary>AudioDirector の BGM の切り替え(同じ曲なら続ける、違えば替える、空なら止める)を確かめる。</summary>
    [TestFixture]
    public class AudioDirectorTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _objects) Object.DestroyImmediate(go);
            _objects.Clear();
        }

        [TestCase("", "", BgmChange.Keep)]
        [TestCase("town", "town", BgmChange.Keep)]
        [TestCase("", "town", BgmChange.Switch)]
        [TestCase("town", "field", BgmChange.Switch)]
        [TestCase("town", "", BgmChange.Stop)]
        [TestCase(null, "", BgmChange.Keep)]
        [TestCase("town", null, BgmChange.Stop)]
        public void 曲の切り替えを判断する(string? current, string? next, BgmChange expected)
        {
            Assert.AreEqual(expected, AudioDirector.DecideBgm(current, next));
        }

        [Test]
        public void マップに入るとその曲を流し同じ曲なら続け違えば替え空なら止める()
        {
            var registry = MapRegistry.LoadFromStreamingAssets();
            var townBgm = registry.Get(100)!.Bgm;
            var fieldBgm = registry.Get(1)!.Bgm;
            var town = WithBgm(TestMaps.Town(), townBgm);
            var sameTune = WithBgm(TestMaps.FlatWithPortals(), townBgm);
            var field = WithBgm(TestMaps.Field(), fieldBgm);
            var silent = WithBgm(TestMaps.Sample(), string.Empty);
            var config = new AudioConfig { BgmFadeSeconds = 0.5f };
            var simulation = TestMaps.NewSimulation(town);
            var director = CreateDirector(simulation, config);

            // 開始時: 町の曲を頭から流す
            Assert.AreEqual(townBgm, director.CurrentBgm);
            Assert.IsNotNull(director.CurrentBgmClip, $"Audio/Bgm/{townBgm}");
            Assert.AreEqual(1, director.BgmStartCount);
            var townClip = director.CurrentBgmClip;

            // 同じ曲のマップへ: 流し直さない
            simulation.ChangeMap(sameTune);
            Assert.AreEqual(townBgm, director.CurrentBgm);
            Assert.AreSame(townClip, director.CurrentBgmClip);
            Assert.AreEqual(1, director.BgmStartCount);

            // 違う曲のマップへ: フェードして替える
            simulation.ChangeMap(field);
            Assert.AreEqual(fieldBgm, director.CurrentBgm);
            Assert.IsNotNull(director.CurrentBgmClip);
            Assert.AreNotSame(townClip, director.CurrentBgmClip);
            Assert.AreEqual(2, director.BgmStartCount);
            Assert.IsTrue(director.IsBgmFading, "前の曲はフェードで消えていく途中");
            director.AdvanceFade(config.BgmFadeSeconds);
            Assert.IsFalse(director.IsBgmFading, "フェードの秒数で替わりきる");
            Assert.AreEqual(config.BgmVolume, CurrentBgmSource(director).volume, 1e-4f);

            // 曲の無いマップへ: 止める
            simulation.ChangeMap(silent);
            Assert.AreEqual(string.Empty, director.CurrentBgm);
            Assert.IsNull(director.CurrentBgmClip);
            Assert.AreEqual(2, director.BgmStartCount);
        }

        [Test]
        public void 曲のファイルが無ければ無音で続け例外にならない()
        {
            var simulation = TestMaps.NewSimulation(WithBgm(TestMaps.Town(), "__missing__"));
            var director = CreateDirector(simulation, AudioConfig.Default);

            Assert.AreEqual("__missing__", director.CurrentBgm);
            Assert.IsNull(director.CurrentBgmClip);
            Assert.AreEqual(0, director.BgmStartCount);

            // 音の無い台帳でも同じ
            var inTown = TestMaps.NewSimulation(WithBgm(TestMaps.Town(), MapRegistry.LoadFromStreamingAssets().Get(100)!.Bgm));
            var withoutFiles = CreateDirector(inTown, AudioConfig.Default, AudioLibrary.Load("Terrace/Audio/__missing__/"));
            Assert.IsNull(withoutFiles.CurrentBgmClip);
        }

        [Test]
        public void 消音はBGMにも効く()
        {
            var hadKey = PlayerPrefs.HasKey(AudioDirector.MutedKey);
            var previous = PlayerPrefs.GetInt(AudioDirector.MutedKey, 0);
            try
            {
                var simulation = TestMaps.NewSimulation(WithBgm(TestMaps.Town(), MapRegistry.LoadFromStreamingAssets().Get(100)!.Bgm));
                var director = CreateDirector(simulation, AudioConfig.Default);

                director.SetMuted(true);
                foreach (var source in director.GetComponents<AudioSource>()) Assert.IsTrue(source.mute, "効果音も BGM も消音");

                director.SetMuted(false);
                foreach (var source in director.GetComponents<AudioSource>()) Assert.IsFalse(source.mute);
            }
            finally
            {
                if (hadKey) PlayerPrefs.SetInt(AudioDirector.MutedKey, previous);
                else PlayerPrefs.DeleteKey(AudioDirector.MutedKey);
            }
        }

        private AudioDirector CreateDirector(GameSimulation simulation, AudioConfig config, AudioLibrary? library = null)
        {
            var go = new GameObject("AudioDirectorTest");
            _objects.Add(go);
            var director = go.AddComponent<AudioDirector>();
            director.Bind(simulation, library ?? AudioLibrary.Load(), config);
            return director;
        }

        private static AudioSource CurrentBgmSource(AudioDirector director)
        {
            foreach (var source in director.GetComponents<AudioSource>())
            {
                if (source.clip != null && source.clip == director.CurrentBgmClip) return source;
            }
            Assert.Fail("今の曲を流している AudioSource が無い");
            return null!;
        }

        private static MapData WithBgm(MapData map, string bgm)
        {
            map.Bgm = bgm;
            return map;
        }
    }
}
