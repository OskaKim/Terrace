using System;
using System.Collections.Concurrent;
using System.Threading;
using Terrace.Shared;

namespace Terrace.Client.Core.Online
{
    /// <summary>
    /// サーバーからの通知の受け箱。MagicOnion にはこれを受信者(IGameHubReceiver)として渡す。
    /// 通知はどのスレッドで届いても列に積むだけで、GameSimulation.Step がメインスレッドで順に取り出して反映する。
    /// </summary>
    public sealed class OnlineInbox : IGameHubReceiver
    {
        private readonly ConcurrentQueue<Action<IGameHubReceiver>> _queue = new ConcurrentQueue<Action<IGameHubReceiver>>();
        private int _disconnected;
        private string _disconnectReason = string.Empty;

        /// <summary>まだ取り出していない通知の数。</summary>
        public int Pending => _queue.Count;

        public bool IsDisconnected => Volatile.Read(ref _disconnected) != 0;

        public string DisconnectReason => _disconnectReason;

        /// <summary>接続が切れたことを知らせる(どのスレッドからでもよい)。</summary>
        public void MarkDisconnected(string reason)
        {
            _disconnectReason = reason;
            Interlocked.Exchange(ref _disconnected, 1);
        }

        /// <summary>積まれた通知を古い順に target へ渡す。渡した数を返す。</summary>
        public int Drain(IGameHubReceiver target, int max = int.MaxValue)
        {
            var count = 0;
            while (count < max && _queue.TryDequeue(out var action))
            {
                action(target);
                count++;
            }
            return count;
        }

        void IGameHubReceiver.OnJoin(PlayerInfo player, MoveState state) => _queue.Enqueue(r => r.OnJoin(player, state));

        void IGameHubReceiver.OnLeave(int playerId) => _queue.Enqueue(r => r.OnLeave(playerId));

        void IGameHubReceiver.OnMove(int playerId, MoveState state) => _queue.Enqueue(r => r.OnMove(playerId, state));

        void IGameHubReceiver.OnEnemySpawn(EnemyState enemy) => _queue.Enqueue(r => r.OnEnemySpawn(enemy));

        void IGameHubReceiver.OnEnemyDamaged(int enemyInstanceId, int hp, int attackerPlayerId, int damage)
            => _queue.Enqueue(r => r.OnEnemyDamaged(enemyInstanceId, hp, attackerPlayerId, damage));

        void IGameHubReceiver.OnEnemyDead(int enemyInstanceId, int killerPlayerId, int[] droppedItemIds)
            => _queue.Enqueue(r => r.OnEnemyDead(enemyInstanceId, killerPlayerId, droppedItemIds));

        void IGameHubReceiver.OnSnapshot(RoomSnapshot snapshot) => _queue.Enqueue(r => r.OnSnapshot(snapshot));

        void IGameHubReceiver.OnEnemyMove(EnemyMoveState[] enemies) => _queue.Enqueue(r => r.OnEnemyMove(enemies));

        void IGameHubReceiver.OnDropSpawn(DropState[] drops) => _queue.Enqueue(r => r.OnDropSpawn(drops));

        void IGameHubReceiver.OnDropRemoved(int dropId, int playerId) => _queue.Enqueue(r => r.OnDropRemoved(dropId, playerId));
    }
}
