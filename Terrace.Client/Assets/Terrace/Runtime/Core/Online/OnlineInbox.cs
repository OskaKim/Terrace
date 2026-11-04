using System;
using System.Collections.Concurrent;
using System.Threading;
using Terrace.Shared;

namespace Terrace.Client.Core.Online
{
    /// <summary>
    /// サーバーからの通知の受け箱。MagicOnion にはこれを受信者(IGameHubReceiver)として渡す。
    /// 通知はどのスレッドで届いても列に積むだけで、GameSimulation.Step がメインスレッドで順に取り出して反映する。
    /// 通知の種類は知らない(<see cref="INotification"/> のまま積む)ので、通知を足してもここは変わらない。
    /// </summary>
    public sealed class OnlineInbox : IGameHubReceiver
    {
        private readonly ConcurrentQueue<INotification> _queue = new ConcurrentQueue<INotification>();
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

        /// <summary>積まれた通知を古い順に apply へ渡す。渡した数を返す。</summary>
        public int Drain(Action<INotification> apply, int max = int.MaxValue)
        {
            var count = 0;
            while (count < max && _queue.TryDequeue(out var notification))
            {
                apply(notification);
                count++;
            }
            return count;
        }

        void IGameHubReceiver.OnNotification(INotification notification)
        {
            if (notification != null) _queue.Enqueue(notification);
        }
    }
}
