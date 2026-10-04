using System;
using System.Collections.Generic;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>鳴らす効果音の種類。どのきっかけで鳴らすかは AudioDirector が決める。</summary>
    public enum SoundEffect
    {
        Jump,
        Swing,
        Hit,
        Kill,
        Damaged,
        Died,
        Pickup,
        Portal,
        ShopOpen,
        ShopClose,
        TradeSucceeded,
        TradeFailed,
    }

    /// <summary>
    /// Resources/Terrace/Audio 配下の音の台帳。効果音の種類とファイルの対応はここにだけ書く。
    /// 効果音を差し替えるときは、Audio/Se/ のファイルを置き換えるか、下の対応表のファイル名を変える。
    /// BGM の対応表は持たない。どの曲を流すかはマップ JSON の bgm が決め、その値が Audio/Bgm/ からの相対パスになる。
    /// ファイルが無い音は null を返す(例外もエラーのログも出さない)。音が 1 つも無くてもゲームは動く。
    /// </summary>
    public sealed class AudioLibrary
    {
        public const string Root = "Terrace/Audio/";
        public const string SoundEffectRoot = Root + "Se/";
        public const string BgmRoot = Root + "Bgm/";

        /// <summary>効果音の対応表(拡張子なしのファイル名。出典は Audio/Se/README.md)。</summary>
        private static readonly Dictionary<SoundEffect, string> SoundEffectFiles = new Dictionary<SoundEffect, string>
        {
            { SoundEffect.Jump, "sfx_movement_jump1" },
            { SoundEffect.Swing, "sfx_wpn_sword1" },
            { SoundEffect.Hit, "impactPunch_medium_000" },
            { SoundEffect.Kill, "sfx_exp_shortest_soft1" },
            { SoundEffect.Damaged, "sfx_damage_hit1" },
            { SoundEffect.Died, "sfx_deathscream_alien6" },
            { SoundEffect.Pickup, "sfx_coin_single2" },
            { SoundEffect.Portal, "sfx_movement_portal5" },
            { SoundEffect.ShopOpen, "open_001" },
            { SoundEffect.ShopClose, "close_001" },
            { SoundEffect.TradeSucceeded, "handleCoins2" },
            { SoundEffect.TradeFailed, "error_001" },
        };

        private readonly string _soundEffectRoot;
        private readonly string _bgmRoot;
        private readonly Dictionary<SoundEffect, AudioClip?> _cache = new Dictionary<SoundEffect, AudioClip?>();
        private readonly Dictionary<string, AudioClip?> _bgmCache = new Dictionary<string, AudioClip?>();

        private AudioLibrary(string root)
        {
            _soundEffectRoot = root + "Se/";
            _bgmRoot = root + "Bgm/";
        }

        /// <summary>効果音の種類をすべて返す。</summary>
        public static IReadOnlyList<SoundEffect> AllSoundEffects { get; } = (SoundEffect[])Enum.GetValues(typeof(SoundEffect));

        /// <summary>Resources/Terrace/Audio から読む。root は試験で「音が無い」状態を作るときだけ変える(その下の Se/ と Bgm/ を読む)。</summary>
        public static AudioLibrary Load(string root = Root) => new AudioLibrary(root);

        /// <summary>対応表にあるファイル名(拡張子なし)。</summary>
        public static string FileName(SoundEffect effect) => SoundEffectFiles[effect];

        /// <summary>その音のクリップ。ファイルが無ければ null。</summary>
        public AudioClip? Get(SoundEffect effect)
        {
            if (_cache.TryGetValue(effect, out var cached)) return cached;
            var clip = SoundEffectFiles.TryGetValue(effect, out var name) ? Resources.Load<AudioClip>(_soundEffectRoot + name) : null;
            _cache[effect] = clip;
            return clip;
        }

        /// <summary>BGM のクリップ。bgm はマップ JSON の値(Audio/Bgm/ からの相対パス、拡張子なし)。空かファイルが無ければ null。</summary>
        public AudioClip? GetBgm(string? bgm)
        {
            if (string.IsNullOrEmpty(bgm)) return null;
            if (_bgmCache.TryGetValue(bgm!, out var cached)) return cached;
            var clip = Resources.Load<AudioClip>(_bgmRoot + bgm);
            _bgmCache[bgm!] = clip;
            return clip;
        }

        /// <summary>全部の効果音を読み込んでおき、読めた数を返す(最初に鳴らすときに引っかからないように)。</summary>
        public int Preload()
        {
            var count = 0;
            foreach (var effect in AllSoundEffects)
            {
                if (Get(effect) != null) count++;
            }
            return count;
        }
    }
}
