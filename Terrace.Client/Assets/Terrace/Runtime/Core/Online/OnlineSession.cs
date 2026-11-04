using System;
using Terrace.Map;
using Terrace.Shared;

namespace Terrace.Client.Core.Online
{
    /// <summary>
    /// オンラインの 1 回の接続。GameSimulation は「オンラインならこれがある」ことだけを知る。
    ///
    ///   送る:  参加(マップに入るたび) / 移動(MoveSender で間引く) / チャット。攻撃と拾うは今のマップの RoomMirror が送る
    ///   受ける: OnlineInbox に積まれた通知を Pump で取り出し、敵と落とし物は今のマップの RoomMirror に、他のプレイヤーは RemotePlayerRegistry に映す。
    ///           チャットはイベントで知らせる(ワールド全体のものなので、スナップショット待ちの間も捨てない)
    ///
    /// 誰が決めるか:
    ///   自分の位置・HP・所持品・メソ・経験値とレベル・店 … このクライアント
    ///   敵の位置・HP・撃破・復活、落とし物の出現・消滅(早い者勝ち) … サーバー
    ///
    /// マップを移ると、前のマップの通知が遅れて届くことがある。新しいマップのスナップショットが届くまでは通知を捨てる。
    /// 接続が切れた印は IsDisconnected で分かる。オフラインへの切り替えは GameSimulation が行う。
    /// </summary>
    public sealed class OnlineSession
    {
        private readonly IOnlineChannel _channel;
        private readonly OnlineInbox _inbox;
        private readonly Func<int, EnemyDefinition?> _enemyLookup;
        private readonly RemotePlayerRegistry _remotePlayers;
        private readonly Action<INotification> _apply;
        private int _mapId;
        private bool _awaitingSnapshot;

        /// <param name="remotePlayers">他のプレイヤーを映す先。見た目が購読し続けるので、切断しても同じものを使い続けられるよう外から渡す。</param>
        public OnlineSession(IOnlineChannel channel, OnlineInbox inbox, Func<int, EnemyDefinition?> enemyLookup, RemotePlayerRegistry remotePlayers)
        {
            _channel = channel;
            _inbox = inbox;
            _enemyLookup = enemyLookup;
            _remotePlayers = remotePlayers;
            _apply = Apply;
        }

        /// <summary>ログインで発行された自分。</summary>
        public PlayerInfo Self => _channel.Self;

        /// <summary>接続先(表示用)。</summary>
        public string ServerAddress => _channel.ServerAddress;

        /// <summary>今いるマップのスナップショットを受け取り済みか。</summary>
        public bool IsSynchronized => Mirror != null && !_awaitingSnapshot;

        /// <summary>今いるマップの世界の権威。マップに入るたびに作り直す。</summary>
        public RoomMirror? Mirror { get; private set; }

        public MoveSender MoveSender { get; } = new MoveSender();

        public bool IsDisconnected => _inbox.IsDisconnected;

        public string DisconnectReason => _inbox.DisconnectReason;

        /// <summary>今いるマップのスナップショットを受け取った。引数は自分以外にそのマップにいた人数。</summary>
        public event Action<int>? Synchronized;

        /// <summary>同じマップに他のプレイヤーがやって来た(スナップショットに入っていた人は含まない)。</summary>
        public event Action<RemotePlayer>? PlayerJoined;

        /// <summary>同じマップから他のプレイヤーが去った。</summary>
        public event Action<RemotePlayer>? PlayerLeft;

        /// <summary>誰か(自分も)のチャットが届いた。</summary>
        public event Action<ChatNotification>? ChatReceived;

        /// <summary>自分のチャットが断られた。</summary>
        public event Action<ChatRejectedNotification>? ChatRejected;

        /// <summary>
        /// マップに入る。新しい RoomMirror を作り、参加を送り、スナップショット待ちにする(前のマップの他のプレイヤーは消す)。
        /// state は参加するときの自分の移動状態。
        /// </summary>
        public RoomMirror JoinMap(MapData map, MoveState state)
        {
            _mapId = map.Id;
            _awaitingSnapshot = true;
            _remotePlayers.Clear();
            Mirror = new RoomMirror(map, _enemyLookup, _channel);
            MoveSender.Reset();
            _channel.Join(map.Id, state);
            MoveSender.MarkSent(state);
            return Mirror;
        }

        /// <summary>受け箱に積まれた通知を古い順に反映する(Step の頭で呼ぶ)。</summary>
        public void Pump() => _inbox.Drain(_apply);

        /// <summary>
        /// ワールド全体へチャットを送る。前後の空白を落とし、空なら送らない(送ったら true)。
        /// 手元では出さない。自分の発言も Server からの通知(<see cref="ChatReceived"/>)で出す(全員で順序が揃う)。
        /// </summary>
        public bool SendChat(string? text)
        {
            var trimmed = text?.Trim() ?? string.Empty;
            if (trimmed.Length == 0) return false;
            _channel.SendChat(trimmed);
            return true;
        }

        /// <summary>今の移動状態を、送るべきときだけ送る(間引き)。</summary>
        public void SendMoveIfNeeded(MoveState state, float dt)
        {
            if (!MoveSender.ShouldSend(state, dt)) return;
            _channel.Move(state);
            MoveSender.MarkSent(state);
        }

        /// <summary>自分の移動状態(送信用)を、移動と状態から組み立てる。</summary>
        public static MoveState MoveStateOf(CharacterMotor motor, PlayerState player)
        {
            MotionState motion;
            if (player.IsDead) motion = MotionState.Dead;
            else if (motor.Mode == MotorMode.Ladder) motion = MotionState.Ladder;
            else if (motor.IsAttackLocked) motion = MotionState.Attack;
            else if (motor.Mode == MotorMode.Air) motion = MotionState.Jump;
            else if (motor.IsCrouching) motion = MotionState.Crouch;
            else if (Math.Abs(motor.VelocityX) > 0.01f) motion = MotionState.Walk;
            else motion = MotionState.Stand;

            return new MoveState
            {
                X = motor.X,
                Y = motor.Y,
                VelocityX = motor.VelocityX,
                VelocityY = motor.VelocityY,
                Facing = RoomMirror.ToFacing(motor.Facing),
                Motion = motion,
            };
        }

        /// <summary>
        /// 取り出した通知を 1 つ映す。スナップショットと他のプレイヤーはここで、敵と落とし物は今のマップの RoomMirror で映す。
        /// スナップショット待ちの間に届いた通知(前のマップの残り)は捨てる。
        /// </summary>
        private void Apply(INotification notification)
        {
            switch (notification)
            {
                case RoomSnapshot snapshot:
                    ApplySnapshot(snapshot);
                    return;
                case ChatNotification chat:
                    ChatReceived?.Invoke(chat);
                    return;
                case ChatRejectedNotification rejected:
                    ChatRejected?.Invoke(rejected);
                    return;
            }

            var mirror = Mirror;
            if (_awaitingSnapshot || mirror == null) return;

            var selfId = _channel.Self.PlayerId;
            switch (notification)
            {
                case PlayerJoinedNotification joined:
                    if (joined.Player.PlayerId == selfId) return;
                    PlayerJoined?.Invoke(_remotePlayers.Upsert(joined.Player, joined.State));
                    return;
                case PlayerLeftNotification left:
                    var removed = _remotePlayers.Remove(left.PlayerId);
                    if (removed != null) PlayerLeft?.Invoke(removed);
                    return;
                case PlayerMovedNotification moved:
                    if (moved.PlayerId == selfId) return;
                    _remotePlayers.ApplyMove(moved.PlayerId, moved.State);
                    return;
                default:
                    mirror.Apply(notification);
                    return;
            }
        }

        private void ApplySnapshot(RoomSnapshot snapshot)
        {
            if (Mirror == null || snapshot.MapId != _mapId) return;

            _awaitingSnapshot = false;
            Mirror.ApplySnapshot(snapshot.Enemies, snapshot.Drops);
            _remotePlayers.Reset(snapshot.Players);
            Synchronized?.Invoke(snapshot.Players.Length);
        }
    }
}
