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

public sealed record AttackResult(AttackOutcome Outcome, int EnemyInstanceId, int Hp, int[] DroppedItemIds)
{
    public static AttackResult Fail(AttackOutcome outcome, int enemyInstanceId) => new(outcome, enemyInstanceId, 0, Array.Empty<int>());
}

/// <summary>
/// 1 マップ = 1 ルーム。MagicOnion に依存しない純 C# クラス。
///
/// - プレイヤー辞書、Join / Leave、位置更新の保持、参加時に現在の全状態を返す Snapshot
/// - 敵の管理: スポーン設定から一定数を湧かせ、HP を持ち、0 になったら死亡して N 秒後に復活
/// - Tick(deltaSeconds) でリスポーンのタイマーを進める(実際の駆動は BackgroundService)
///
/// 状態変化はすべて <see cref="IRoomEventSink"/> へ通知する。通知はロック内で行い、順序を保証する。
/// </summary>
public sealed class Room
{
    private readonly object _gate = new();
    private readonly Dictionary<int, RoomPlayer> _players = new();
    private readonly Dictionary<int, RoomEnemy> _enemies = new();
    private readonly IMoveValidator _moveValidator;
    private readonly IRoomEventSink _sink;
    private readonly Random _random;
    private int _nextEnemyInstanceId = 1;

    public Room(int mapId, IEnumerable<EnemySpawnConfig> spawns, IMoveValidator moveValidator, IRoomEventSink sink, Random? random = null)
    {
        MapId = mapId;
        _moveValidator = moveValidator;
        _sink = sink;
        _random = random ?? Random.Shared;

        foreach (var spawn in spawns)
        {
            for (var i = 0; i < spawn.Count; i++)
            {
                var enemy = new RoomEnemy(_nextEnemyInstanceId++, spawn);
                _enemies.Add(enemy.InstanceId, enemy);
            }
        }
    }

    public int MapId { get; }

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

    /// <summary>敵にダメージを与える(サーバー権威)。HP が 0 になれば死亡し、ドロップを抽選して復活タイマーを開始する。</summary>
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
            _sink.OnEnemyDead(enemy.InstanceId, playerId, drops);
            return new AttackResult(AttackOutcome.Killed, enemy.InstanceId, 0, drops);
        }
    }

    /// <summary>リスポーンのタイマーを進める。復活した敵を返す。</summary>
    public IReadOnlyList<RoomEnemy> Tick(float deltaSeconds)
    {
        var respawned = new List<RoomEnemy>();
        lock (_gate)
        {
            foreach (var enemy in _enemies.Values)
            {
                if (!enemy.IsDead) continue;

                enemy.RespawnTimer -= deltaSeconds;
                if (enemy.RespawnTimer > 0f) continue;

                enemy.Respawn();
                _sink.OnEnemySpawned(enemy.ToState());
                respawned.Add(enemy);
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
        };
    }

    private int[] RollDrops(RoomEnemy enemy)
    {
        var drops = new List<int>();
        foreach (var itemId in enemy.DropItemIds)
        {
            if (_random.NextDouble() < enemy.DropRate)
            {
                drops.Add(itemId);
            }
        }
        return drops.ToArray();
    }
}
