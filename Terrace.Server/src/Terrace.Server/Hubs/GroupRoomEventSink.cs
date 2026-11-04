using Cysharp.Runtime.Multicast;
using Terrace.Server.Rooms;
using Terrace.Shared;

namespace Terrace.Server.Hubs;

/// <summary>Room の通知をグループ配信に変換する。ルーム破棄時に Dispose され、グループも閉じる。</summary>
public sealed class GroupRoomEventSink(IMulticastSyncGroup<int, IGameHubReceiver> group) : IRoomEventSink, IDisposable
{
    public void Broadcast(INotification notification) => group.All.OnNotification(notification);

    public void BroadcastExcept(int excludePlayerId, INotification notification) => group.Except(excludePlayerId).OnNotification(notification);

    public void Dispose() => group.Dispose();
}
