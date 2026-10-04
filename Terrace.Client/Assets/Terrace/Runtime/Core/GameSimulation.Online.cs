using System;
using System.Collections.Generic;
using Terrace.Client.Core.Online;
using Terrace.Shared;

namespace Terrace.Client.Core
{
    /// <summary>
    /// GameSimulation のオンライン部分。
    ///
    ///   送る:  参加(マップに入るたび) / 移動(MoveSender で間引く) / 攻撃(当たった相手) / 拾う(近くの落とし物)
    ///   受ける: OnlineInbox に積まれた通知を Step の頭で取り出し、世界(LocalWorld)と他のプレイヤー(RemotePlayers)に映す
    ///
    /// 誰が決めるか:
    ///   自分の位置・HP・所持品・メソ・経験値とレベル・店 … このクライアント
    ///   敵の位置・HP・撃破・復活、落とし物の出現・消滅(早い者勝ち) … サーバー
    ///
    /// マップを移ると、前のマップの通知が遅れて届くことがある。新しいマップのスナップショットが届くまでは通知を捨てる。
    /// 接続が切れたらオフラインに切り替え、今いるマップの敵を自分で湧かせて遊び続けられるようにする。
    /// </summary>
    public sealed partial class GameSimulation
    {
        private IOnlineChannel? _channel;
        private OnlineInbox? _inbox;
        private OnlineReceiver? _receiver;
        private readonly MoveSender _moveSender = new MoveSender();
        private readonly HashSet<int> _pendingPickups = new HashSet<int>();
        private bool _awaitingSnapshot;

        public bool IsOnline => _channel != null;

        /// <summary>サーバーから今いるマップのスナップショットを受け取り済みか。</summary>
        public bool IsSynchronized => IsOnline && !_awaitingSnapshot;

        /// <summary>オンラインでの自分。オフラインでは null。</summary>
        public PlayerInfo? Self => _channel?.Self;

        public string? ServerAddress => _channel?.ServerAddress;

        /// <summary>同じマップにいる他のプレイヤー。</summary>
        public RemotePlayerRegistry RemotePlayers { get; } = new RemotePlayerRegistry();

        public MoveSender MoveSender => _moveSender;

        /// <summary>世界(敵と落とし物)が別物に差し替わった(オフラインへの切り替えなど、マップはそのまま)。</summary>
        public event Action<LocalWorld>? WorldReplaced;

        /// <summary>今いるマップのスナップショットを受け取った。</summary>
        public event Action? Synchronized;

        /// <summary>接続が切れてオフラインに切り替わった。引数は理由。</summary>
        public event Action<string>? WentOffline;

        /// <summary>今の自分の移動状態(送信用)。</summary>
        public MoveState CurrentMoveState()
        {
            MotionState motion;
            if (Player.IsDead) motion = MotionState.Dead;
            else if (Motor.Mode == MotorMode.Ladder) motion = MotionState.Ladder;
            else if (Motor.IsAttackLocked) motion = MotionState.Attack;
            else if (Motor.Mode == MotorMode.Air) motion = MotionState.Jump;
            else if (Motor.IsCrouching) motion = MotionState.Crouch;
            else if (Math.Abs(Motor.VelocityX) > 0.01f) motion = MotionState.Walk;
            else motion = MotionState.Stand;

            return new MoveState
            {
                X = Motor.X,
                Y = Motor.Y,
                VelocityX = Motor.VelocityX,
                VelocityY = Motor.VelocityY,
                Facing = LocalWorld.ToFacing(Motor.Facing),
                Motion = motion,
            };
        }

        /// <summary>接続を手放してオフラインで続ける(切断の通知を受けたときも呼ばれる)。</summary>
        public void GoOffline(string reason)
        {
            if (!IsOnline) return;

            _channel = null;
            _inbox = null;
            _receiver = null;
            _awaitingSnapshot = false;
            _pendingPickups.Clear();
            RemotePlayers.Clear();

            World = GetOrCreateWorld(Map);
            Messages.Add(Time, $"サーバーとの接続が切れた。オフラインで続けます ({reason})");
            WorldReplaced?.Invoke(World);
            WentOffline?.Invoke(reason);
        }

        private void JoinCurrentMap()
        {
            _awaitingSnapshot = true;
            _pendingPickups.Clear();
            _moveSender.Reset();
            var state = CurrentMoveState();
            _channel!.Join(Map.Id, state);
            _moveSender.MarkSent(state);
        }

        private void PumpOnline()
        {
            var inbox = _inbox!;
            _receiver ??= new OnlineReceiver(this);
            inbox.Drain(_receiver);
            if (inbox.IsDisconnected) GoOffline(inbox.DisconnectReason);
        }

        private void SendMoveIfNeeded(float dt)
        {
            var state = CurrentMoveState();
            if (!_moveSender.ShouldSend(state, dt)) return;
            _channel!.Move(state);
            _moveSender.MarkSent(state);
        }

        private void RequestPickup()
        {
            var drop = World.FindPickupCandidate(Motor.X, Motor.Y, PlayerConfig.PickupRange);
            if (drop == null || !_pendingPickups.Add(drop.DropId)) return;
            _channel!.Pickup(drop.DropId);
        }

        /// <summary>取り出した通知を GameSimulation に反映する。Inbox の外(テスト)から直接呼んでもよい。</summary>
        private sealed class OnlineReceiver : IGameHubReceiver
        {
            private readonly GameSimulation _sim;

            public OnlineReceiver(GameSimulation sim)
            {
                _sim = sim;
            }

            private int SelfId => _sim._channel?.Self.PlayerId ?? 0;

            /// <summary>スナップショット待ちの間に届いた通知(前のマップの残り)は捨てる。</summary>
            private bool Ignoring => _sim._awaitingSnapshot || !_sim.IsOnline;

            public void OnSnapshot(RoomSnapshot snapshot)
            {
                if (!_sim.IsOnline || snapshot.MapId != _sim.Map.Id) return;

                _sim._awaitingSnapshot = false;
                _sim._pendingPickups.Clear();
                _sim.World.ApplySnapshot(snapshot.Enemies, snapshot.Drops);
                _sim.RemotePlayers.Reset(snapshot.Players);
                var others = snapshot.Players.Length == 0 ? "ほかに誰もいない" : $"ほかに {snapshot.Players.Length} 人";
                _sim.Messages.Add(_sim.Time, $"{_sim.Map.Name} に入った ({others})");
                _sim.Synchronized?.Invoke();
            }

            public void OnJoin(PlayerInfo player, MoveState state)
            {
                if (Ignoring || player.PlayerId == SelfId) return;
                _sim.RemotePlayers.Upsert(player, state);
                _sim.Messages.Add(_sim.Time, $"{player.Name} がやって来た");
            }

            public void OnLeave(int playerId)
            {
                if (Ignoring) return;
                var removed = _sim.RemotePlayers.Remove(playerId);
                if (removed != null) _sim.Messages.Add(_sim.Time, $"{removed.Name} が去った");
            }

            public void OnMove(int playerId, MoveState state)
            {
                if (Ignoring || playerId == SelfId) return;
                _sim.RemotePlayers.ApplyMove(playerId, state);
            }

            public void OnEnemySpawn(EnemyState enemy)
            {
                if (Ignoring) return;
                _sim.World.ApplyEnemySpawn(enemy);
            }

            public void OnEnemyDamaged(int enemyInstanceId, int hp, int attackerPlayerId, int damage)
            {
                if (Ignoring) return;
                var enemy = _sim.World.ApplyEnemyDamaged(enemyInstanceId, hp);
                if (enemy != null && attackerPlayerId == SelfId && hp > 0)
                {
                    _sim.Messages.Add(_sim.Time, $"{enemy.Definition.Name} に {damage} ダメージ (残り {hp})");
                }
            }

            public void OnEnemyDead(int enemyInstanceId, int killerPlayerId, int[] droppedItemIds)
            {
                if (Ignoring) return;
                var alreadyDead = _sim.World.FindEnemy(enemyInstanceId)?.IsDead ?? true;
                var enemy = _sim.World.ApplyEnemyDead(enemyInstanceId);
                if (enemy == null || alreadyDead) return;

                if (killerPlayerId == SelfId)
                {
                    _sim.GrantKillReward(enemy, droppedItemIds);
                }
                else
                {
                    var killer = _sim.RemotePlayers.Find(killerPlayerId)?.Name ?? $"player{killerPlayerId}";
                    _sim.Messages.Add(_sim.Time, $"{killer} が {enemy.Definition.Name} を倒した");
                }
            }

            public void OnEnemyMove(EnemyMoveState[] enemies)
            {
                if (Ignoring) return;
                _sim.World.ApplyEnemyMove(enemies);
            }

            public void OnDropSpawn(DropState[] drops)
            {
                if (Ignoring) return;
                _sim.World.ApplyDropSpawn(drops);
            }

            public void OnDropRemoved(int dropId, int playerId)
            {
                if (Ignoring) return;
                _sim._pendingPickups.Remove(dropId);
                var drop = _sim.World.ApplyDropRemoved(dropId);
                if (drop == null || playerId != SelfId) return;

                _sim.Player.Inventory.Add(drop.ItemId);
                _sim.Messages.Add(_sim.Time, $"{_sim.ItemName(drop.ItemId)} を拾った");
                _sim.ItemPickedUp?.Invoke(drop);
            }
        }
    }
}
