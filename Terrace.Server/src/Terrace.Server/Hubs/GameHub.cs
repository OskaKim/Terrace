using MagicOnion.Server.Hubs;
using Terrace.Server.Rooms;
using Terrace.Shared;

namespace Terrace.Server.Hubs;

/// <summary>
/// StreamingHub の実装。ロジックは持たず、純 C# の <see cref="RoomManager"/> / <see cref="Room"/> を呼ぶだけ。
/// 1 接続 = 1 プレイヤー。配信は Room → <see cref="GroupRoomEventSink"/> → グループ、の経路で行う。
/// </summary>
public sealed class GameHub(RoomManager rooms, RoomGroupRegistry groups, ILogger<GameHub> logger)
    : StreamingHubBase<IGameHub, IGameHubReceiver>, IGameHub
{
    private int _mapId;
    private int _playerId = -1;
    private string _playerName = string.Empty;

    public ValueTask JoinAsync(int mapId, PlayerInfo self)
    {
        if (_playerId >= 0)
        {
            LeaveCore();
        }

        // 先にルームへ入れてから(ルームとグループが確実に存在する状態で)接続をグループに登録する
        var snapshot = rooms.Join(mapId, self);
        groups.GetOrAdd(mapId).Add(self.PlayerId, Client);

        _mapId = mapId;
        _playerId = self.PlayerId;
        _playerName = self.Name;
        logger.LogInformation("Join map={MapId} player={Player} (他 {Players} 人, 敵 {Enemies} 体)",
            mapId, self, snapshot.Players.Length, snapshot.Enemies.Length);

        Client.OnSnapshot(snapshot);
        return default;
    }

    public ValueTask LeaveAsync()
    {
        LeaveCore();
        return default;
    }

    public ValueTask MoveAsync(MoveState state)
    {
        if (_playerId < 0) return default;

        var result = rooms.Find(_mapId)?.Move(_playerId, state) ?? MoveResult.UnknownPlayer;
        if (result != MoveResult.Accepted)
        {
            logger.LogWarning("Move rejected map={MapId} player={Player} result={Result}", _mapId, _playerName, result);
        }
        return default;
    }

    public ValueTask AttackAsync(int enemyInstanceId, int damage)
    {
        if (_playerId < 0) return default;

        var result = rooms.Find(_mapId)?.Attack(_playerId, enemyInstanceId, damage)
                     ?? AttackResult.Fail(AttackOutcome.UnknownPlayer, enemyInstanceId);
        logger.LogInformation("Attack map={MapId} player={Player} enemy#{Enemy} damage={Damage} => {Outcome} hp={Hp} drops=[{Drops}]",
            _mapId, _playerName, enemyInstanceId, damage, result.Outcome, result.Hp, string.Join(",", result.DroppedItemIds));
        return default;
    }

    protected override ValueTask OnDisconnected()
    {
        LeaveCore();
        return default;
    }

    private void LeaveCore()
    {
        if (_playerId < 0) return;

        groups.GetOrAdd(_mapId).Remove(_playerId);
        rooms.Leave(_mapId, _playerId);
        logger.LogInformation("Leave map={MapId} player={Player}#{PlayerId}", _mapId, _playerName, _playerId);
        _playerId = -1;
    }
}
