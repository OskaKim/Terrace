using Terrace.Map;
using Terrace.Shared;

namespace Terrace.Server.Rooms;

public enum MoveResult
{
    Accepted,
    Rejected,
    UnknownPlayer,
}

public enum AttackOutcome
{
    Damaged,
    Killed,
    EnemyNotFound,
    AlreadyDead,
    UnknownPlayer,
}

public enum PickupOutcome
{
    Picked,
    DropNotFound,
    UnknownPlayer,
}

public sealed record AttackResult(AttackOutcome Outcome, int EnemyInstanceId, int Hp, int[] DroppedItemIds)
{
    public static AttackResult Fail(AttackOutcome outcome, int enemyInstanceId) => new(outcome, enemyInstanceId, 0, Array.Empty<int>());
}

/// <summary>
/// 1 マップ = 1 ルーム。MagicOnion に依存しない純 C# クラス。
///
/// - プレイヤー辞書、Join / Leave、位置更新の保持、参加時に現在の全状態を返す Snapshot
/// - 敵の管理: スポーン設定から一定数を湧かせ、足場の上を巡回し、HP を持ち、0 になったら死亡して N 秒後に復活
/// - 落とし物: 敵の死亡時に抽選して置き、早い者勝ちで拾う。60 秒で消える
/// - Tick(deltaSeconds) で巡回・リスポーン・落とし物の寿命を進め、位置は 0.2 秒ごとにまとめて通知
///
/// 状態変化はすべて <see cref="IRoomEventSink"/> へ通知する。通知はロック内で行い、順序を保証する。
/// </summary>
public sealed class Room
{
    public const float DropLifetimeSeconds = 60f;
    public const float EnemyMoveBroadcastInterval = 0.2f;

    private readonly object _gate = new();
    private readonly Dictionary<int, RoomPlayer> _players = new();
    private readonly Dictionary<int, RoomEnemy> _enemies = new();
    private readonly Dictionary<int, RoomDrop> _drops = new();
    private readonly IMoveValidator _moveValidator;
    private readonly IRoomEventSink _sink;
    private readonly Random _random;
    private int _nextEnemyInstanceId = 1;
    private int _nextDropId = 1;
    private float _moveBroadcastTimer;

    public Room(int mapId, MapData? map, IEnumerable<EnemySpawnConfig> spawns, IMoveValidator moveValidator, IRoomEventSink sink, Random? random = null)
    {
        MapId = mapId;
        Map = map;
        _moveValidator = moveValidator;
        _sink = sink;
        _random = random ?? Random.Shared;

        foreach (var spawn in spawns)
        {
            var ground = map?.FindFootholdBelow(spawn.X, spawn.Y + 0.5f);
            for (var i = 0; i < spawn.Count; i++)
            {
                var enemy = new RoomEnemy(_nextEnemyInstanceId++, spawn, ground);
                _enemies.Add(enemy.InstanceId, enemy);
            }
        }
    }

    public int MapId { get; }

    /// <summary>マップ構造(無ければ敵は動かない)。</summary>
    public MapData? Map { get; }

    public IRoomEventSink Sink => _sink;

    public int PlayerCount
    {
        get { lock (_gate) return _players.Count; }
    }

    public bool IsEmpty => PlayerCount == 0;

    public IReadOnlyList<RoomPlayer> Players
    {
        get { lock (_gate) return _players.Values.ToList(); }
    }

    public IReadOnlyList<RoomEnemy> Enemies
    {
        get { lock (_gate) return _enemies.Values.OrderBy(e => e.InstanceId).ToList(); }
    }

    public IReadOnlyList<RoomDrop> Drops
    {
        get { lock (_gate) return _drops.Values.OrderBy(d => d.DropId).ToList(); }
    }

    public RoomPlayer? FindPlayer(int playerId)
    {
        lock (_gate) return _players.TryGetValue(playerId, out var player) ? player : null;
    }

    public RoomEnemy? FindEnemy(int enemyInstanceId)
    {
        lock (_gate) return _enemies.TryGetValue(enemyInstanceId, out var enemy) ? enemy : null;
    }

    /// <summary>参加する。同じ PlayerId が既にいれば置き換える(再接続)。戻り値は本人を除いた現在の全状態。</summary>
    public RoomSnapshot Join(PlayerInfo player, MoveState? initialState = null)
    {
        lock (_gate)
        {
            var state = initialState ?? new MoveState();
            _players[player.PlayerId] = new RoomPlayer(player, state);
            var snapshot = CreateSnapshotCore(excludePlayerId: player.PlayerId);
            _sink.OnPlayerJoined(player, state);
            return snapshot;
        }
    }

    /// <summary>退出する。いなければ false。</summary>
    public bool Leave(int playerId)
    {
        lock (_gate)
        {
            if (!_players.Remove(playerId)) return false;
            _sink.OnPlayerLeft(playerId);
            return true;
        }
    }

    /// <summary>クライアント権威の移動状態を受け取り、検証フックを通れば保持して他へ通知する。</summary>
    public MoveResult Move(int playerId, MoveState state)
    {
        lock (_gate)
        {
            if (!_players.TryGetValue(playerId, out var player)) return MoveResult.UnknownPlayer;
            if (!_moveValidator.IsAcceptable(player, state)) return MoveResult.Rejected;

            player.State = state;
            _sink.OnPlayerMoved(playerId, state);
            return MoveResult.Accepted;
        }
    }

    /// <summary>敵にダメージを与える(サーバー権威)。HP が 0 になれば死亡し、ドロップを抽選して置き、復活タイマーを開始する。</summary>
    public AttackResult Attack(int playerId, int enemyInstanceId, int damage)
    {
        lock (_gate)
        {
            if (!_players.ContainsKey(playerId)) return AttackResult.Fail(AttackOutcome.UnknownPlayer, enemyInstanceId);
            if (!_enemies.TryGetValue(enemyInstanceId, out var enemy)) return AttackResult.Fail(AttackOutcome.EnemyNotFound, enemyInstanceId);
            if (enemy.IsDead) return AttackResult.Fail(AttackOutcome.AlreadyDead, enemyInstanceId);

            damage = Math.Max(0, damage);
            enemy.Hp = Math.Max(0, enemy.Hp - damage);
            _sink.OnEnemyDamaged(enemy.InstanceId, enemy.Hp, playerId, damage);

            if (enemy.Hp > 0)
            {
                return new AttackResult(AttackOutcome.Damaged, enemy.InstanceId, enemy.Hp, Array.Empty<int>());
            }

            enemy.IsDead = true;
            enemy.RespawnTimer = enemy.RespawnSeconds;
            var drops = RollDrops(enemy);
            var itemIds = drops.Select(d => d.ItemId).ToArray();
            if (drops.Count > 0) _sink.OnDropSpawned(drops.Select(d => d.ToState()).ToList());
            _sink.OnEnemyDead(enemy.InstanceId, playerId, itemIds);
            return new AttackResult(AttackOutcome.Killed, enemy.InstanceId, 0, itemIds);
        }
    }

    /// <summary>落ちているアイテムを拾う(早い者勝ち)。</summary>
    public PickupOutcome Pickup(int playerId, int dropId)
    {
        lock (_gate)
        {
            if (!_players.ContainsKey(playerId)) return PickupOutcome.UnknownPlayer;
            if (!_drops.Remove(dropId)) return PickupOutcome.DropNotFound;
            _sink.OnDropRemoved(dropId, playerId);
            return PickupOutcome.Picked;
        }
    }

    /// <summary>巡回・リスポーン・落とし物の寿命を進める。復活した敵を返す。</summary>
    public IReadOnlyList<RoomEnemy> Tick(float deltaSeconds)
    {
        var respawned = new List<RoomEnemy>();
        lock (_gate)
        {
            foreach (var enemy in _enemies.Values)
            {
                if (enemy.IsDead)
                {
                    enemy.RespawnTimer -= deltaSeconds;
                    if (enemy.RespawnTimer > 0f) continue;

                    enemy.Respawn();
                    _sink.OnEnemySpawned(enemy.ToState());
                    respawned.Add(enemy);
                    continue;
                }

                enemy.Patrol(deltaSeconds);
            }

            _moveBroadcastTimer += deltaSeconds;
            if (_moveBroadcastTimer >= EnemyMoveBroadcastInterval)
            {
                _moveBroadcastTimer = 0f;
                var moving = _enemies.Values.Where(e => !e.IsDead && e.CanMove).Select(e => e.ToMoveState()).ToList();
                if (moving.Count > 0 && _players.Count > 0) _sink.OnEnemyMoved(moving);
            }

            foreach (var drop in _drops.Values.ToList())
            {
                drop.RemainingSeconds -= deltaSeconds;
                if (drop.RemainingSeconds > 0f) continue;
                _drops.Remove(drop.DropId);
                _sink.OnDropRemoved(drop.DropId, 0);
            }
        }
        return respawned;
    }

    /// <summary>現在の全状態。excludePlayerId を指定するとそのプレイヤーを含めない。</summary>
    public RoomSnapshot CreateSnapshot(int? excludePlayerId = null)
    {
        lock (_gate) return CreateSnapshotCore(excludePlayerId);
    }

    private RoomSnapshot CreateSnapshotCore(int? excludePlayerId)
    {
        return new RoomSnapshot
        {
            MapId = MapId,
            Players = _players.Values
                .Where(p => excludePlayerId is null || p.Info.PlayerId != excludePlayerId.Value)
                .Select(p => p.ToSnapshot())
                .ToArray(),
            Enemies = _enemies.Values
                .OrderBy(e => e.InstanceId)
                .Select(e => e.ToState())
                .ToArray(),
            Drops = _drops.Values
                .OrderBy(d => d.DropId)
                .Select(d => d.ToState())
                .ToArray(),
        };
    }

    private List<RoomDrop> RollDrops(RoomEnemy enemy)
    {
        var itemIds = new List<int>();
        foreach (var itemId in enemy.DropItemIds)
        {
            if (_random.NextDouble() < enemy.DropRate) itemIds.Add(itemId);
        }

        var drops = new List<RoomDrop>();
        for (var i = 0; i < itemIds.Count; i++)
        {
            var offset = (i - (itemIds.Count - 1) * 0.5f) * 0.6f;
            var drop = new RoomDrop(_nextDropId++, itemIds[i], enemy.X + offset, enemy.Y, DropLifetimeSeconds);
            _drops[drop.DropId] = drop;
            drops.Add(drop);
        }
        return drops;
    }
}
