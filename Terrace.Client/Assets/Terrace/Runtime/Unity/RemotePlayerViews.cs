using System.Collections.Generic;
using Terrace.Client.Core.Online;
using Terrace.Map;
using Terrace.Shared;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// 他のプレイヤー 1 人の見た目。自分と同じ絵を色違いにして、RemotePlayer の表示位置と状態(歩く/跳ぶ/攻撃…)をなぞる。
    /// 名前は HudView が頭の下に描く。
    /// </summary>
    public sealed class RemotePlayerView : MonoBehaviour
    {
        /// <summary>プレイヤー ID ごとの色(同じ ID なら誰の画面でも同じ色)。</summary>
        private static readonly Color[] Tints =
        {
            new Color(0.70f, 0.85f, 1.00f),
            new Color(1.00f, 0.75f, 0.85f),
            new Color(1.00f, 0.95f, 0.60f),
            new Color(0.80f, 1.00f, 0.75f),
            new Color(0.90f, 0.80f, 1.00f),
        };

        private RemotePlayer? _player;
        private CharacterArt? _art;
        private SpriteRenderer? _body;
        private SpriteAnimator? _animator;
        private SpriteRenderer? _swing;
        private Color _tint = Color.white;
        private float _lastY;

        public RemotePlayer? Player => _player;

        public static Color TintOf(int playerId) => Tints[Mathf.Abs(playerId) % Tints.Length];

        public void Bind(RemotePlayer player, ArtLibrary? art)
        {
            _player = player;
            _art = art?.Player;
            _tint = TintOf(player.PlayerId);

            if (_art != null)
            {
                var bodyGo = new GameObject("Body");
                bodyGo.transform.SetParent(transform, false);
                _body = bodyGo.AddComponent<SpriteRenderer>();
                _body.sprite = _art.Stand;
                _body.sortingOrder = 9;
                _animator = bodyGo.AddComponent<SpriteAnimator>();
                var star = art!.Get("Items/star");
                _swing = SpriteFactory.CreateRenderer("Swing", transform, star ?? SpriteFactory.Circle(), Color.white, 0.7f, 0.7f, 8);
                _swing.transform.localPosition = new Vector3(0.95f, 0.3f, 0f);
            }
            else
            {
                _body = SpriteFactory.CreateRenderer("Body", transform, SpriteFactory.RoundedRect(), _tint, PlayerView.FallbackWidth, PlayerView.FallbackHeight, 9);
                _swing = SpriteFactory.CreateRenderer("Swing", transform, SpriteFactory.Circle(), new Color(1f, 1f, 1f, 0.6f), 1.4f, 1.0f, 8);
                _swing.transform.localPosition = new Vector3(1.0f, 0.3f, 0f);
            }

            _swing.enabled = false;
            _lastY = player.Y;
            Sync();
        }

        public void Sync()
        {
            if (_player == null || _body == null || _swing == null) return;

            transform.position = new Vector3(_player.X, _player.Y, 0.1f);
            transform.localScale = new Vector3(_player.Facing == Direction.Right ? 1f : -1f, 1f, 1f);

            var motion = _player.Motion;
            if (_art != null && _animator != null)
            {
                switch (motion)
                {
                    case MotionState.Dead:
                        _animator.Show(_art.Hurt);
                        break;
                    case MotionState.Ladder:
                        _animator.Play(_art.Climb);
                        _animator.Paused = Mathf.Abs(_player.Y - _lastY) < 0.0001f;
                        break;
                    case MotionState.Jump:
                        _animator.Show(_art.Jump);
                        break;
                    case MotionState.Crouch:
                        _animator.Show(_art.Duck);
                        break;
                    case MotionState.Walk:
                        _animator.Paused = false;
                        _animator.Play(_art.Walk);
                        break;
                    default:
                        _animator.Show(_art.Stand);
                        break;
                }
            }

            _body.color = motion == MotionState.Dead ? new Color(0.5f, 0.5f, 0.5f, 0.6f) : _tint;
            _swing.enabled = motion == MotionState.Attack;
            if (_swing.enabled)
            {
                _swing.transform.localRotation = Quaternion.Euler(0f, 0f, (Time.time * 720f) % 360f);
            }
            _lastY = _player.Y;
        }
    }

    /// <summary>RemotePlayerRegistry の増減に合わせて見た目を作り・消し、毎フレーム位置を合わせる。</summary>
    public sealed class RemotePlayersViewSync
    {
        private readonly Transform _root;
        private readonly ArtLibrary? _art;
        private readonly RemotePlayerRegistry _registry;
        private readonly Dictionary<int, RemotePlayerView> _views = new Dictionary<int, RemotePlayerView>();

        public RemotePlayersViewSync(Transform root, RemotePlayerRegistry registry, ArtLibrary? art)
        {
            _root = root;
            _art = art;
            _registry = registry;
            registry.Added += OnAdded;
            registry.Removed += OnRemoved;
            foreach (var player in registry.All) OnAdded(player);
        }

        public int Count => _views.Count;

        public void Sync()
        {
            foreach (var view in _views.Values) view.Sync();
        }

        public void Dispose()
        {
            _registry.Added -= OnAdded;
            _registry.Removed -= OnRemoved;
            foreach (var view in _views.Values)
            {
                if (view != null) Object.Destroy(view.gameObject);
            }
            _views.Clear();
        }

        private void OnAdded(RemotePlayer player)
        {
            if (_views.ContainsKey(player.PlayerId)) return;
            var go = new GameObject($"RemotePlayer {player.Name}#{player.PlayerId}");
            go.transform.SetParent(_root, false);
            var view = go.AddComponent<RemotePlayerView>();
            view.Bind(player, _art);
            _views[player.PlayerId] = view;
        }

        private void OnRemoved(RemotePlayer player)
        {
            if (!_views.TryGetValue(player.PlayerId, out var view)) return;
            _views.Remove(player.PlayerId);
            if (view != null) Object.Destroy(view.gameObject);
        }
    }
}
