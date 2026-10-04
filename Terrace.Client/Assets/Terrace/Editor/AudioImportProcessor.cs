using UnityEditor;
using UnityEngine;

namespace Terrace.Client.Editor
{
    /// <summary>
    /// Assets/Resources/Terrace/Audio 配下の音を、手で設定しなくても揃った設定で取り込む。
    /// - 効果音(Audio/Se/): 短く何度も鳴らすので、読み込み時に展開しておく
    /// - BGM(Audio/Bgm/): 長いので、鳴らしながらストリーミングで読む
    /// </summary>
    public sealed class AudioImportProcessor : AssetPostprocessor
    {
        public const string AudioRoot = "Assets/Resources/Terrace/Audio/";
        public const string SoundEffectRoot = AudioRoot + "Se/";
        public const string BgmRoot = AudioRoot + "Bgm/";

        private void OnPreprocessAudio()
        {
            var path = assetPath.Replace('\\', '/');
            if (!path.StartsWith(AudioRoot)) return;

            var importer = (AudioImporter)assetImporter;
            var settings = importer.defaultSampleSettings;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            if (path.StartsWith(BgmRoot))
            {
                settings.loadType = AudioClipLoadType.Streaming;
                settings.preloadAudioData = false;
            }
            else if (path.StartsWith(SoundEffectRoot))
            {
                // 最初に鳴らすときに読み込みで遅れないよう、クリップを読んだ時点で中身も読んでおく
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.preloadAudioData = true;
            }
            else
            {
                return;
            }
            importer.defaultSampleSettings = settings;
        }
    }
}
