using Terrace.Map;
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
    private readonly Func<int, MapData?> _mapLookup;
    private readonly IMoveValidator _moveValidator;
    private readonly Func<int, IRoomEventSink> _sinkFactory;

    /// <param name="spawnProvider">mapId → そのマップのスポーン設定。</param>
    /// <param name="mapLookup">mapId → マップ構造(敵の巡回に使う。無ければ null)。</param>
    /// <param name="moveValidator">移動の検証フック。</param>
    /// <param name="sinkFactory">mapId → ルームの通知先。IDisposable ならルーム破棄時に Dispose する。</param>
    public RoomManager(Func<int, IEnumerable<EnemySpawnConfig>> spawnProvider, Func<int, MapData?> mapLookup, IMoveValidator moveValidator, Func<int, IRoomEventSink> sinkFactory)
    {
        _spawnProvider = spawnProvider;
        _mapLookup = mapLookup;
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

    /// <summary>この位置でマップに参加してよいか(壊れた値や境界の外なら false。<see cref="IMoveValidator"/>)。</summary>
    public bool CanJoinAt(int mapId, MoveState state) => _moveValidator.IsAcceptable(_mapLookup(mapId), null, state);

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
                // TickAll が破棄の前に複製した配列から Tick しても、閉じた Sink へ配信しないように
                room.Close();
                (room.Sink as IDisposable)?.Dispose();
                RoomRemoved?.Invoke(room);
            }
            return left;
        }
    }

    /// <summary>
    /// 全ルームの巡回・リスポーン・落とし物の寿命を進める。
    /// 1 つのルームの例外(配信先の失敗など)は <see cref="TickFailed"/> で知らせ、ほかのルームを進め続ける。
    /// </summary>
    public void TickAll(float deltaSeconds)
    {
        Room[] rooms;
        lock (_gate) rooms = _rooms.Values.ToArray();
        foreach (var room in rooms)
        {
            try
            {
                room.Tick(deltaSeconds);
            }
            catch (Exception ex)
            {
                TickFailed?.Invoke(room, ex);
            }
        }
    }

    public event Action<Room>? RoomCreated;

    /// <summary>ルームの Tick が例外を投げた。そのルームは残し、次の Tick でまた進める。</summary>
    public event Action<Room, Exception>? TickFailed;
    public event Action<Room>? RoomRemoved;

    private Room GetOrCreateCore(int mapId)
    {
        if (_rooms.TryGetValue(mapId, out var existing)) return existing;

        var room = new Room(mapId, _mapLookup(mapId), _spawnProvider(mapId), _moveValidator, _sinkFactory(mapId));
        _rooms.Add(mapId, room);
        RoomCreated?.Invoke(room);
        return room;
    }
}
