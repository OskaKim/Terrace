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

        public static AudioConfig Default => new AudioConfig();
    }

    /// <summary>
    /// 効果音を鳴らす係。GameSimulation のイベントを購読し、自分の動作と自分の身に起きたことにだけ音を付ける
    /// (他のプレイヤーの動作や、他の人が敵を倒したことには鳴らさない)。Core は音を知らない。
    ///
    /// 消音は PlayerPrefs に覚え、次に起動したときも引き継ぐ。
    /// </summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        public const string MutedKey = "terrace.audioMuted";

        private GameSimulation? _simulation;
        private AudioLibrary? _library;
        private AudioSource? _source;
        private AudioConfig _config = AudioConfig.Default;

        public bool IsMuted { get; private set; }

        /// <summary>最後に鳴らそうとした音(消音中やファイルが無くて鳴らなかったものも含む)。試験と調べもの用。</summary>
        public SoundEffect? LastRequested { get; private set; }

        /// <summary>読めた効果音の数。</summary>
        public int AvailableCount { get; private set; }

        public void Bind(GameSimulation simulation, AudioLibrary library, AudioConfig? config = null)
        {
            Unbind();
            _simulation = simulation;
            _library = library;
            _config = config ?? AudioConfig.Default;

            if (_source == null)
            {
                _source = gameObject.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.spatialBlend = 0f;
            }
            IsMuted = LoadMuted();
            _source.mute = IsMuted;
            AvailableCount = library.Preload();

            simulation.Jumped += OnJumped;
            simulation.Attacked += OnAttacked;
            simulation.EnemyKilled += OnEnemyKilled;
            simulation.PlayerDamaged += OnPlayerDamaged;
            simulation.PlayerDied += OnPlayerDied;
            simulation.ItemPickedUp += OnItemPickedUp;
            simulation.PortalUsed += OnPortalUsed;
            simulation.ShopOpened += OnShopOpened;
            simulation.ShopClosed += OnShopClosed;
            simulation.ShopTraded += OnShopTraded;
        }

        private void OnDestroy() => Unbind();

        private void Unbind()
        {
            var simulation = _simulation;
            if (simulation == null) return;
            simulation.Jumped -= OnJumped;
            simulation.Attacked -= OnAttacked;
            simulation.EnemyKilled -= OnEnemyKilled;
            simulation.PlayerDamaged -= OnPlayerDamaged;
            simulation.PlayerDied -= OnPlayerDied;
            simulation.ItemPickedUp -= OnItemPickedUp;
            simulation.PortalUsed -= OnPortalUsed;
            simulation.ShopOpened -= OnShopOpened;
            simulation.ShopClosed -= OnShopClosed;
            simulation.ShopTraded -= OnShopTraded;
            _simulation = null;
        }

        // ---- 消音 ----

        public void ToggleMute() => SetMuted(!IsMuted);

        public void SetMuted(bool muted)
        {
            IsMuted = muted;
            if (_source != null) _source.mute = muted;
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

        // ---- 鳴らす ----

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
    }
}
