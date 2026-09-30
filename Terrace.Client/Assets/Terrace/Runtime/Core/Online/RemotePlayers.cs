using System;
using System.Collections.Generic;
using Terrace.Map;
using Terrace.Shared;

namespace Terrace.Client.Core.Online
{
    /// <summary>
    /// 同じマップにいる他のプレイヤー 1 人。
    /// サーバーから届いた最新の移動状態(State)と、画面に出す位置(X, Y)を分けて持つ。
    /// 移動の通知は 0.1 秒おき程度なので、表示位置は少し先読みした地点へ毎フレーム滑らかに寄せる。
    /// </summary>
    public sealed class RemotePlayer
    {
        /// <summary>1 秒あたりの追従率。大きいほど早く追いつく。</summary>
        public const float FollowRate = 12f;

        /// <summary>これ以上離れていたら寄せずに瞬間移動する(復活・ポータルなど)。</summary>
        public const float SnapDistance = 4f;

        /// <summary>歩行中、最後の通知から何秒ぶんまで速度で先読みするか。</summary>
        public const float MaxLead = 0.12f;

        public RemotePlayer(PlayerInfo info, MoveState state)
        {
            PlayerId = info.PlayerId;
            Name = info.Name;
            State = state;
            X = state.X;
            Y = state.Y;
        }

        public int PlayerId { get; }
        public string Name { get; internal set; }

        /// <summary>最後に届いた移動状態。</summary>
        public MoveState State { get; private set; }

        /// <summary>表示位置。</summary>
        public float X { get; private set; }
        public float Y { get; private set; }

        public Direction Facing => State.Facing == Terrace.Shared.Facing.Right ? Direction.Right : Direction.Left;
        public MotionState Motion => State.Motion;

        /// <summary>最後の通知からの経過秒数。</summary>
        public float SinceUpdate { get; private set; }

        internal void Apply(MoveState state)
        {
            State = state;
            SinceUpdate = 0f;
        }

        internal void Tick(float dt)
        {
            SinceUpdate += dt;
            var lead = Math.Min(SinceUpdate, MaxLead);
            var targetX = State.X + (State.Motion == MotionState.Walk ? State.VelocityX * lead : 0f);
            var targetY = State.Y;

            if (Math.Abs(targetX - X) > SnapDistance || Math.Abs(targetY - Y) > SnapDistance)
            {
                X = targetX;
                Y = targetY;
                return;
            }

            var t = 1f - (float)Math.Exp(-FollowRate * dt);
            X += (targetX - X) * t;
            Y += (targetY - Y) * t;
        }

        public override string ToString() => $"{Name}#{PlayerId} ({X:F1}, {Y:F1}) {Motion}";
    }

    /// <summary>同じマップにいる他のプレイヤーの一覧。見た目はイベントで増減を知る。</summary>
    public sealed class RemotePlayerRegistry
    {
        private readonly Dictionary<int, RemotePlayer> _players = new Dictionary<int, RemotePlayer>();
        private readonly List<RemotePlayer> _ordered = new List<RemotePlayer>();

        public int Count => _ordered.Count;

        /// <summary>参加順。</summary>
        public IReadOnlyList<RemotePlayer> All => _ordered;

        public event Action<RemotePlayer>? Added;
        public event Action<RemotePlayer>? Removed;

        public RemotePlayer? Find(int playerId) => _players.TryGetValue(playerId, out var player) ? player : null;

        /// <summary>いなければ加え、いれば状態と名前を更新する。</summary>
        public RemotePlayer Upsert(PlayerInfo info, MoveState state)
        {
            if (_players.TryGetValue(info.PlayerId, out var existing))
            {
                existing.Name = info.Name;
                existing.Apply(state);
                return existing;
            }

            var player = new RemotePlayer(info, state);
            _players.Add(info.PlayerId, player);
            _ordered.Add(player);
            Added?.Invoke(player);
            return player;
        }

        /// <summary>移動を反映する。知らない人なら仮の名前で加える(参加の通知より先に移動が届いた場合)。</summary>
        public RemotePlayer ApplyMove(int playerId, MoveState state)
        {
            if (_players.TryGetValue(playerId, out var player))
            {
                player.Apply(state);
                return player;
            }
            return Upsert(new PlayerInfo { PlayerId = playerId, Name = $"player{playerId}" }, state);
        }

        public RemotePlayer? Remove(int playerId)
        {
            if (!_players.TryGetValue(playerId, out var player)) return null;
            _players.Remove(playerId);
            _ordered.Remove(player);
            Removed?.Invoke(player);
            return player;
        }

        /// <summary>全員を入れ替える(スナップショット)。</summary>
        public void Reset(IReadOnlyList<PlayerSnapshot> players)
        {
            Clear();
            foreach (var snapshot in players) Upsert(snapshot.Info, snapshot.State);
        }

        public void Clear()
        {
            for (var i = _ordered.Count - 1; i >= 0; i--) Remove(_ordered[i].PlayerId);
        }

        public void Tick(float dt)
        {
            foreach (var player in _ordered) player.Tick(dt);
        }
    }
}
