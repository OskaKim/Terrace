using NUnit.Framework;
using Terrace.Client.Unity;
using UnityEngine;

namespace Terrace.Client.Tests.EditMode
{
    /// <summary>Resources に置いた効果音が AudioClip として読めること、無い音は黙って null になることを確かめる。</summary>
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
        }
    }
}
