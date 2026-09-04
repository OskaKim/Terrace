using Cysharp.Runtime.Multicast;
using Terrace.Server.Rooms;
using Terrace.Shared;

namespace Terrace.Server.Hubs;

/// <summary>
/// mapId ごとの配信グループ(MagicOnion / Multicaster)。キーは PlayerId。
/// Hub は接続の追加・削除に、Room の通知先(<see cref="GroupRoomEventSink"/>)は配信に、同じグループを使う。
/// </summary>
public sealed class RoomGroupRegistry(IMulticastGroupProvider groupProvider)
{
    public static string GroupName(int mapId) => $"map:{mapId}";

    public IMulticastSyncGroup<int, IGameHubReceiver> GetOrAdd(int mapId)
        => groupProvider.GetOrAddSynchronousGroup<int, IGameHubReceiver>(GroupName(mapId));

    public IRoomEventSink CreateSink(int mapId) => new GroupRoomEventSink(GetOrAdd(mapId));
}
