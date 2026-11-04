using Terrace.Shared;

namespace Terrace.Server.Rooms;

/// <summary>
/// ルームで起きた状態変化の通知先。Room は MagicOnion を知らず、この口だけを呼ぶ。
/// 通知の種類は知らない(どの通知も <see cref="INotification"/> のまま渡す)ので、通知を足してもここは変わらない。
/// サーバーでは <see cref="Terrace.Server.Hubs.GroupRoomEventSink"/> がグループ配信に変換し、テストでは記録用の実装を差す。
/// </summary>
public interface IRoomEventSink
{
    /// <summary>ルームの全員へ。</summary>
    void Broadcast(INotification notification);

    /// <summary>ルームの、<paramref name="excludePlayerId"/> 以外の全員へ(本人の参加・移動を本人に返さないため)。</summary>
    void BroadcastExcept(int excludePlayerId, INotification notification);
}
