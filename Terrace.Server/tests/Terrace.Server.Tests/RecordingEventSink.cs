using Terrace.Server.Rooms;
using Terrace.Shared;

namespace Terrace.Server.Tests;

/// <summary>Room からの通知を、届く先(全員か、本人以外か)と一緒に記録するテスト用の Sink。通知の種類は知らない。</summary>
public sealed class RecordingEventSink : IRoomEventSink, IDisposable
{
    /// <summary>送った通知 1 件。<paramref name="ExcludePlayerId"/> が null なら全員へ、あればその人以外へ。</summary>
    public sealed record Sent(INotification Notification, int? ExcludePlayerId);

    public List<Sent> All { get; } = new();
    public bool Disposed { get; private set; }

    /// <summary>この型の通知を、送った順に。</summary>
    public IReadOnlyList<T> OfType<T>() where T : INotification => All.Select(s => s.Notification).OfType<T>().ToList();

    /// <summary>この型の最初の通知が、送った通知の何番目か(無ければ -1)。</summary>
    public int IndexOf<T>() where T : INotification => All.FindIndex(s => s.Notification is T);

    public IReadOnlyList<PlayerMovedNotification> Moves => OfType<PlayerMovedNotification>();
    public IReadOnlyList<EnemyState> Spawned => OfType<EnemySpawnedNotification>().Select(n => n.Enemy).ToList();
    public IReadOnlyList<EnemyMoveState[]> EnemyMoves => OfType<EnemiesMovedNotification>().Select(n => n.Enemies).ToList();
    public IReadOnlyList<DropState> DropsSpawned => OfType<DropsSpawnedNotification>().SelectMany(n => n.Drops).ToList();
    public IReadOnlyList<(int DropId, int PlayerId)> DropsRemoved => OfType<DropRemovedNotification>().Select(n => (n.DropId, n.PlayerId)).ToList();

    /// <summary>true にすると敵の復活の通知で例外を投げる(配信先の失敗の代わり)。</summary>
    public bool FailOnEnemySpawned { get; set; }

    public void Broadcast(INotification notification) => Record(notification, null);

    public void BroadcastExcept(int excludePlayerId, INotification notification) => Record(notification, excludePlayerId);

    public void Dispose() => Disposed = true;

    private void Record(INotification notification, int? excludePlayerId)
    {
        if (FailOnEnemySpawned && notification is EnemySpawnedNotification) throw new InvalidOperationException("配信に失敗した(テスト)");
        All.Add(new Sent(notification, excludePlayerId));
    }
}
