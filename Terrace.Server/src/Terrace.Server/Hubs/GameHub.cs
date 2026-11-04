using Grpc.Core;
using MagicOnion;
using MagicOnion.Server.Hubs;
using Terrace.Server.Accounts;
using Terrace.Server.Rooms;
using Terrace.Shared;

namespace Terrace.Server.Hubs;

/// <summary>
/// StreamingHub の実装。ロジックは持たず、純 C# の <see cref="RoomManager"/> / <see cref="Room"/> を呼ぶだけ。
/// 1 接続 = 1 プレイヤー。配信は Room → <see cref="GroupRoomEventSink"/> → グループ、の経路で行う。
/// 参加できるのはログインで発行した ID だけ。同じ ID の接続が 2 つになったら、後から来た方に置き換える(<see cref="PlayerConnections"/>)。
/// </summary>
public sealed class GameHub(RoomManager rooms, RoomGroupRegistry groups, AccountRegistry accounts, PlayerConnections connections, ILogger<GameHub> logger)
    : StreamingHubBase<IGameHub, IGameHubReceiver>, IGameHub
{
    /// <summary>この接続を見分ける鍵(<see cref="PlayerConnections"/> の持ち主の印)。</summary>
    private readonly object _connection = new();
    private int _mapId;
    private int _playerId = -1;
    private string _playerName = string.Empty;

    /// <summary>参加していて、同じ ID の後から来た接続に置き換えられていない。</summary>
    private bool IsActive => _playerId >= 0 && connections.IsOwner(_playerId, _connection);

    public ValueTask JoinAsync(JoinRequest request)
    {
        if (request?.Self is null)
        {
            throw new ReturnStatusException(StatusCode.InvalidArgument, "参加の要求に自分(Self)がありません");
        }

        var mapId = request.MapId;
        var self = request.Self;
        var state = request.State;
        if (!accounts.IsIssued(self.PlayerId))
        {
            logger.LogWarning("Join rejected map={MapId} player={Player}: ログインで発行していない PlayerId", mapId, self);
            throw new ReturnStatusException(StatusCode.PermissionDenied, $"PlayerId {self.PlayerId} はログインで発行していません");
        }

        if (_playerId >= 0)
        {
            LeaveCore();
        }

        RoomSnapshot snapshot = null!;
        connections.Join(self.PlayerId, _connection, mapId, replacedMapId =>
        {
            if (replacedMapId is { } previous)
            {
                // 同じ ID の先の接続を外す。同じマップならルームの中は Join が置き換えるので、配信のグループからだけ外す
                groups.GetOrAdd(previous).Remove(self.PlayerId);
                if (previous != mapId) rooms.Leave(previous, self.PlayerId);
                logger.LogWarning("Join map={MapId} player={Player}: 同じ ID の先の接続(map={Previous})を置き換えます", mapId, self, previous);
            }

            // 先にルームへ入れてから(ルームとグループが確実に存在する状態で)接続をグループに登録する
            snapshot = rooms.Join(mapId, self, state);
            groups.GetOrAdd(mapId).Add(self.PlayerId, Client);
        });

        _mapId = mapId;
        _playerId = self.PlayerId;
        _playerName = self.Name;
        logger.LogInformation("Join map={MapId} player={Player} (他 {Players} 人, 敵 {Enemies} 体, 落とし物 {Drops} 個)",
            mapId, self, snapshot.Players.Length, snapshot.Enemies.Length, snapshot.Drops.Length);

        Client.OnNotification(snapshot);
        return default;
    }

    public ValueTask LeaveAsync()
    {
        LeaveCore();
        return default;
    }

    public void Move(MoveState state)
    {
        if (!IsActive || state is null) return;

        var result = rooms.Find(_mapId)?.Move(_playerId, state) ?? MoveResult.UnknownPlayer;
        if (result != MoveResult.Accepted)
        {
            logger.LogWarning("Move rejected map={MapId} player={Player} result={Result}", _mapId, _playerName, result);
        }
    }

    public void Attack(AttackRequest request)
    {
        if (!IsActive || request is null) return;

        var result = rooms.Find(_mapId)?.Attack(_playerId, request.EnemyInstanceId, request.Damage)
                     ?? AttackResult.Fail(AttackOutcome.UnknownPlayer, request.EnemyInstanceId);
        logger.LogInformation("Attack map={MapId} player={Player} enemy#{Enemy} damage={Damage} => {Outcome} hp={Hp} drops=[{Drops}]",
            _mapId, _playerName, request.EnemyInstanceId, request.Damage, result.Outcome, result.Hp, string.Join(",", result.DroppedItemIds));
    }

    public void Pickup(PickupRequest request)
    {
        if (!IsActive || request is null) return;

        var result = rooms.Find(_mapId)?.Pickup(_playerId, request.DropId) ?? PickupOutcome.UnknownPlayer;
        logger.LogInformation("Pickup map={MapId} player={Player} drop#{Drop} => {Outcome}", _mapId, _playerName, request.DropId, result);
    }

    protected override ValueTask OnDisconnected()
    {
        LeaveCore();
        return default;
    }

    private void LeaveCore()
    {
        if (_playerId < 0) return;

        // 後から来た接続に置き換えられていれば、その接続をグループからもルームからも消さない
        var left = connections.Leave(_playerId, _connection, () =>
        {
            groups.GetOrAdd(_mapId).Remove(_playerId);
            rooms.Leave(_mapId, _playerId);
        });
        if (left) logger.LogInformation("Leave map={MapId} player={Player}#{PlayerId}", _mapId, _playerName, _playerId);
        else logger.LogInformation("Leave map={MapId} player={Player}#{PlayerId}: 同じ ID の後から来た接続に置き換えられていたので、ルームには触らない", _mapId, _playerName, _playerId);
        _playerId = -1;
    }
}
