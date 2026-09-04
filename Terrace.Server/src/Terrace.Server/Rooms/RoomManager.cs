using Terrace.Shared;

namespace Terrace.Server.Rooms;

/// <summary>
/// mapId でルームを取得・生成し、空になったら破棄する。純 C#。
/// Join / Leave はマネージャのロック内で行い、「空判定と同時に誰かが入る」競合を防ぐ。
/// </summary>
public sealed class RoomManager
{
    private readonly object _gate = new();
    private readonly Dictionary<int, Room> _rooms = new();
    private readonly Func<int, IEnumerable<EnemySpawnConfig>> _spawnProvider;
    private readonly IMoveValidator _moveValidator;
    private readonly Func<int, IRoomEventSink> _sinkFactory;

    /// <param name="spawnProvider">mapId → その マップのスポーン設定。</param>
    /// <param name="moveValidator">移動の検証フック。</param>
    /// <param name="sinkFactory">mapId → ルームの通知先。IDisposable ならルーム破棄時に Dispose する。</param>
    public RoomManager(Func<int, IEnumerable<EnemySpawnConfig>> spawnProvider, IMoveValidator moveValidator, Func<int, IRoomEventSink> sinkFactory)
    {
        _spawnProvider = spawnProvider;
        _moveValidator = moveValidator;
        _sinkFactory = sinkFactory;
    }

    public IReadOnlyList<Room> Rooms
    {
        get { lock (_gate) return _rooms.Values.OrderBy(r => r.MapId).ToList(); }
    }

    public Room GetOrCreate(int mapId)
    {
        lock (_gate) return GetOrCreateCore(mapId);
    }

    public Room? Find(int mapId)
    {
        lock (_gate) return _rooms.TryGetValue(mapId, out var room) ? room : null;
    }

    /// <summary>ルームが無ければ作って参加する。</summary>
    public RoomSnapshot Join(int mapId, PlayerInfo player, MoveState? initialState = null)
    {
        lock (_gate) return GetOrCreateCore(mapId).Join(player, initialState);
    }

    /// <summary>退出し、ルームが空になったら破棄する。退出できたら true。</summary>
    public bool Leave(int mapId, int playerId)
    {
        lock (_gate)
        {
            if (!_rooms.TryGetValue(mapId, out var room)) return false;

            var left = room.Leave(playerId);
            if (room.IsEmpty)
            {
                _rooms.Remove(mapId);
                (room.Sink as IDisposable)?.Dispose();
                RoomRemoved?.Invoke(room);
            }
            return left;
        }
    }

    /// <summary>全ルームのリスポーンタイマーを進める。</summary>
    public void TickAll(float deltaSeconds)
    {
        Room[] rooms;
        lock (_gate) rooms = _rooms.Values.ToArray();
        foreach (var room in rooms)
        {
            room.Tick(deltaSeconds);
        }
    }

    public event Action<Room>? RoomCreated;
    public event Action<Room>? RoomRemoved;

    private Room GetOrCreateCore(int mapId)
    {
        if (_rooms.TryGetValue(mapId, out var existing)) return existing;

        var room = new Room(mapId, _spawnProvider(mapId), _moveValidator, _sinkFactory(mapId));
        _rooms.Add(mapId, room);
        RoomCreated?.Invoke(room);
        return room;
    }
}
