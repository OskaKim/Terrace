using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>Animator アセットを使わない、こま送りの最小実装。</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteAnimator : MonoBehaviour
    {
        private SpriteRenderer? _renderer;
        private SpriteClip? _clip;
        private Sprite? _still;
        private float _time;

        /// <summary>true の間はこまを進めない(はしごで止まっているときなど)。</summary>
        public bool Paused;

        public float Speed = 1f;

        public SpriteClip? Clip => _clip;

        private void Awake()
        {
            _renderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>クリップを再生する。同じクリップなら続きから。</summary>
        public void Play(SpriteClip clip)
        {
            if (ReferenceEquals(_clip, clip)) return;
            _clip = clip;
            _still = null;
            _time = 0f;
            Apply();
        }

        /// <summary>1 枚絵を表示する(クリップは止める)。</summary>
        public void Show(Sprite sprite)
        {
            _clip = null;
            if (ReferenceEquals(_still, sprite)) return;
            _still = sprite;
            if (_renderer != null) _renderer.sprite = sprite;
        }

        private void Update()
        {
            if (_clip == null || _clip.Length <= 1) return;
            if (!Paused) _time += Time.deltaTime * _clip.Fps * Speed;
            Apply();
        }

        private void Apply()
        {
            if (_clip == null || _renderer == null) return;
            var count = _clip.Length;
            var index = _clip.Loop ? ((int)_time) % count : Mathf.Min((int)_time, count - 1);
            if (index < 0) index = 0;
            var sprite = _clip.Frames[index];
            if (!ReferenceEquals(_renderer.sprite, sprite)) _renderer.sprite = sprite;
        }
    }
}
