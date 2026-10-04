using System;
using Terrace.Client.Core;
using Terrace.Map;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>音の数値。</summary>
    public sealed class AudioConfig
    {
        /// <summary>効果音の音量(0〜1)。</summary>
        public float SoundEffectVolume { get; set; } = 0.7f;

        /// <summary>BGM の音量(0〜1)。</summary>
        public float BgmVolume { get; set; } = 0.5f;

        /// <summary>BGM を替えるときのフェードの秒数(前の曲が消え、次の曲が上がりきるまで)。0 ならすぐ替える。</summary>
        public float BgmFadeSeconds { get; set; } = 1f;

        public static AudioConfig Default => new AudioConfig();
    }

    /// <summary>マップに入ったときに BGM をどうするか。</summary>
    public enum BgmChange
    {
        /// <summary>今の曲を頭から流し直さずに続ける(無音なら無音のまま)。</summary>
        Keep,
        /// <summary>フェードして別の曲に替える。</summary>
        Switch,
        /// <summary>フェードして止める。</summary>
        Stop,
    }

    /// <summary>
    /// 効果音と BGM を鳴らす係。GameSimulation のイベントを購読する。Core は音を知らない。
    /// - 効果音: 自分の動作と自分の身に起きたことにだけ付ける(他のプレイヤーの動作や、他の人が敵を倒したことには鳴らさない)
    /// - BGM: マップに入ったとき(開始時と MapChanged)に、そのマップの bgm を繰り返し流す。
    ///   オンラインとオフラインの切り替え(WorldReplaced)はマップが変わらないので購読せず、曲はそのまま続く
    ///
    /// 消音は効果音と BGM の両方に効く。PlayerPrefs に覚え、次に起動したときも引き継ぐ。
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public const string MutedKey = "terrace.audioMuted";

        private GameSimulation? _simulation;
        private AudioLibrary? _library;
        private AudioSource? _source;
        private AudioConfig _config = AudioConfig.Default;

        // BGM は 2 つの AudioSource で受け渡す。替えるときは今の曲を下げながら、次の曲を上げる
        private AudioSource? _bgmSource;
        private AudioSource? _bgmFadingSource;
        private float _fadeProgress = 1f;
        private float _fadingStartVolume;

        public bool IsMuted { get; private set; }

        /// <summary>最後に鳴らそうとした音(消音中やファイルが無くて鳴らなかったものも含む)。試験と調べもの用。</summary>
        public SoundEffect? LastRequested { get; private set; }

        /// <summary>読めた効果音の数。</summary>
        public int AvailableCount { get; private set; }

        /// <summary>今のマップの bgm(ファイルが無くて無音のときも名前は持つ)。空なら無音。</summary>
        public string CurrentBgm { get; private set; } = string.Empty;

        /// <summary>今流している BGM のクリップ。無音なら null。</summary>
        public AudioClip? CurrentBgmClip => _bgmSource != null ? _bgmSource.clip : null;

        /// <summary>BGM を頭から流し始めた回数。同じ曲のマップへ移っても増えない。試験と調べもの用。</summary>
        public int BgmStartCount { get; private set; }

        /// <summary>前の曲がまだフェードで消えていく途中か。</summary>
        public bool IsBgmFading => _fadeProgress < 1f;

        public void Bind(GameSimulation simulation, AudioLibrary library, AudioConfig? config = null)
        {
            Unbind();
            _simulation = simulation;
            _library = library;
            _config = config ?? AudioConfig.Default;

            if (_source == null) _source = CreateSource(loop: false);
            if (_bgmSource == null) _bgmSource = CreateSource(loop: true);
            if (_bgmFadingSource == null) _bgmFadingSource = CreateSource(loop: true);
            IsMuted = LoadMuted();
            ApplyMute();
            AvailableCount = library.Preload();

            simulation.Jumped += OnJumped;
            simulation.Combat.Attacked += OnAttacked;
            simulation.Rewards.EnemyKilled += OnEnemyKilled;
            simulation.Life.PlayerDamaged += OnPlayerDamaged;
            simulation.Life.PlayerDied += OnPlayerDied;
            simulation.Looting.ItemPickedUp += OnItemPickedUp;
            simulation.Travel.PortalUsed += OnPortalUsed;
            simulation.Travel.MapChanged += OnMapChanged;
            simulation.Trading.ShopOpened += OnShopOpened;
            simulation.Trading.ShopClosed += OnShopClosed;
            simulation.Trading.ShopTraded += OnShopTraded;

            PlayBgm(simulation.Map.Bgm);
        }

        private AudioSource CreateSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.loop = loop;
            return source;
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            var simulation = _simulation;
            if (simulation == null) return;
            simulation.Jumped -= OnJumped;
            simulation.Combat.Attacked -= OnAttacked;
            simulation.Rewards.EnemyKilled -= OnEnemyKilled;
            simulation.Life.PlayerDamaged -= OnPlayerDamaged;
            simulation.Life.PlayerDied -= OnPlayerDied;
            simulation.Looting.ItemPickedUp -= OnItemPickedUp;
            simulation.Travel.PortalUsed -= OnPortalUsed;
            simulation.Travel.MapChanged -= OnMapChanged;
            simulation.Trading.ShopOpened -= OnShopOpened;
            simulation.Trading.ShopClosed -= OnShopClosed;
            simulation.Trading.ShopTraded -= OnShopTraded;
            _simulation = null;
        }

        // ---- 消音 ----

        public void ToggleMute() => SetMuted(!IsMuted);

        public void SetMuted(bool muted)
        {
            IsMuted = muted;
            ApplyMute();
            try
            {
                PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
                PlayerPrefs.Save();
            }
            catch (Exception)
            {
                // PlayerPrefs が使えない環境でも、この起動の間は消音を保つ
            }
        }

        // BGM は消音中も流し続ける(戻したときに曲の続きから聞こえるように)
        private void ApplyMute()
        {
            if (_source != null) _source.mute = IsMuted;
            if (_bgmSource != null) _bgmSource.mute = IsMuted;
            if (_bgmFadingSource != null) _bgmFadingSource.mute = IsMuted;
        }

        private static bool LoadMuted()
        {
            try
            {
                return PlayerPrefs.GetInt(MutedKey, 0) != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ---- 効果音 ----

        /// <summary>音を 1 つ鳴らす。消音中とファイルの無い音は黙って飛ばす。</summary>
        public void Play(SoundEffect effect)
        {
            LastRequested = effect;
            if (IsMuted || _source == null || _library == null) return;
            var clip = _library.Get(effect);
            if (clip == null) return;
            _source.PlayOneShot(clip, _config.SoundEffectVolume);
        }

        private void OnJumped() => Play(SoundEffect.Jump);

        private void OnAttacked(AttackOutcome outcome)
        {
            // 振る音は当たらなくても鳴らし、当たればさらに当たった音を重ねる
            Play(SoundEffect.Swing);
            if (outcome.Hit) Play(SoundEffect.Hit);
        }

        private void OnEnemyKilled(EnemyEntity enemy) => Play(SoundEffect.Kill);

        private void OnPlayerDamaged(int damage) => Play(SoundEffect.Damaged);

        private void OnPlayerDied() => Play(SoundEffect.Died);

        private void OnItemPickedUp(ItemDrop drop) => Play(SoundEffect.Pickup);

        private void OnPortalUsed(Portal from, Portal? to) => Play(SoundEffect.Portal);

        private void OnShopOpened(ShopSession shop) => Play(SoundEffect.ShopOpen);

        private void OnShopClosed() => Play(SoundEffect.ShopClose);

        private void OnShopTraded(ShopResult result) => Play(result == ShopResult.Ok ? SoundEffect.TradeSucceeded : SoundEffect.TradeFailed);

        // ---- BGM ----

        /// <summary>今の曲(current)から次のマップの曲(next)へ移るときにどうするか。空は無音。</summary>
        public static BgmChange DecideBgm(string? current, string? next)
        {
            var from = current ?? string.Empty;
            var to = next ?? string.Empty;
            if (string.Equals(from, to, StringComparison.Ordinal)) return BgmChange.Keep;
            return to.Length == 0 ? BgmChange.Stop : BgmChange.Switch;
        }

        /// <summary>マップの bgm を流す。同じ曲なら続け、違えばフェードして替え、空なら止める。ファイルが無ければ無音で続ける。</summary>
        public void PlayBgm(string? bgm)
        {
            var next = bgm ?? string.Empty;
            var change = DecideBgm(CurrentBgm, next);
            if (change == BgmChange.Keep) return;

            CurrentBgm = next;
            FadeOutCurrentBgm();
            if (change == BgmChange.Stop || _bgmSource == null || _library == null) return;

            var clip = _library.GetBgm(next);
            if (clip == null) return;
            _bgmSource.clip = clip;
            _bgmSource.volume = _config.BgmFadeSeconds > 0f ? 0f : _config.BgmVolume;
            _bgmSource.Play();
            BgmStartCount++;
        }

        // 今の曲を「消えていく側」へ回し、空いた側を次の曲に使う。前のフェードの残りは打ち切る
        private void FadeOutCurrentBgm()
        {
            if (_bgmSource == null || _bgmFadingSource == null) return;
            _bgmFadingSource.Stop();
            _bgmFadingSource.clip = null;

            var fading = _bgmSource;
            _bgmSource = _bgmFadingSource;
            _bgmFadingSource = fading;
            _fadingStartVolume = fading.volume;
            _fadeProgress = 0f;
            AdvanceFade(0f);
        }

        private void Update() => AdvanceFade(Time.unscaledDeltaTime);

        /// <summary>フェードを進める。毎フレーム Update から呼ばれる(試験では手で進める)。</summary>
        public void AdvanceFade(float deltaSeconds)
        {
            if (_bgmSource == null || _bgmFadingSource == null || _fadeProgress >= 1f) return;
            var seconds = _config.BgmFadeSeconds;
            _fadeProgress = seconds > 0f ? Mathf.Min(1f, _fadeProgress + deltaSeconds / seconds) : 1f;
            _bgmSource.volume = _config.BgmVolume * _fadeProgress;
            _bgmFadingSource.volume = _fadingStartVolume * (1f - _fadeProgress);
            if (_fadeProgress >= 1f)
            {
                _bgmFadingSource.Stop();
                _bgmFadingSource.clip = null;
            }
        }

        private void OnMapChanged(MapData previous, MapData next) => PlayBgm(next.Bgm);
    }
}
