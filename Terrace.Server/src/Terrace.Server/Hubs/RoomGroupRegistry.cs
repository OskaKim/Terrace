using Cysharp.Runtime.Multicast;
using Terrace.Server.Rooms;
using Terrace.Shared;

namespace Terrace.Server.Hubs;

/// <summary>
/// mapId ごとの配信グループと、ワールド全体の配信グループ(MagicOnion / Multicaster)。キーは PlayerId。
/// Hub は接続の追加・削除に、Room の通知先(<see cref="GroupRoomEventSink"/>)は配信に、同じグループを使う。
/// 同じキーを 2 度足すと両方に届いてしまうので、足す前に必ず外す(同じ ID の接続の置き換え)。
/// </summary>
public sealed class RoomGroupRegistry(IMulticastGroupProvider groupProvider)
{
    public const string WorldGroupName = "world";

    public static string GroupName(int mapId) => $"map:{mapId}";

    /// <summary>どこかのマップに参加している全員(ワールド全体のチャットの配り先)。</summary>
    public IMulticastSyncGroup<int, IGameHubReceiver> World
        => groupProvider.GetOrAddSynchronousGroup<int, IGameHubReceiver>(WorldGroupName);

    public IMulticastSyncGroup<int, IGameHubReceiver> GetOrAdd(int mapId)
        => groupProvider.GetOrAddSynchronousGroup<int, IGameHubReceiver>(GroupName(mapId));

    public IRoomEventSink CreateSink(int mapId) => new GroupRoomEventSink(GetOrAdd(mapId));
}
