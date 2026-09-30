using System;
using System.Collections.Generic;
using Terrace.Map;
using Terrace.Shared;

namespace Terrace.Client.Core
{
    /// <summary>敵と落とし物の状態を誰が決めるか。</summary>
    public enum WorldAuthority
    {
        /// <summary>オフライン: このクライアントが湧き・巡回・HP・撃破・ドロップ・復活をすべて計算する。</summary>
        Local,

        /// <summary>オンライン: サーバー(Terrace.Server の Room)が決め、ここは届いた結果を映すだけ。</summary>
        Server,
    }

    /// <summary>
    /// 1 マップ分の敵と落とし物の世界。
    ///
    /// オフライン(Local)ではサーバーの Room と同じ規則で自分で動かす:
    /// 湧き点から湧き、HP を持ち、0 になったら死亡してドロップを抽選し、N 秒後に復活する。敵は足場の上を端で折り返しながら巡回する。
    ///
    /// オンライン(Server)では何も自分で決めない。Apply* でサーバーの通知を受け取り、
    /// 位置は届いた地点へ滑らかに寄せ、攻撃は「当たった相手」を探すだけ(HP はサーバーの返事で減る)。
    /// </summary>
    public sealed class LocalWorld
    {
        public const float DropLifetimeSeconds = 60f;
        public const float HitFlashSeconds = 0.15f;

        /// <summary>オンライン: 表示位置をサーバー位置へ寄せる速さ(1 秒あたりの追従率)。</summary>
        public const float NetFollowRate = 10f;

        /// <summary>オンライン: これ以上離れていたら寄せずに瞬間移動する。</summary>
        public const float NetSnapDistance = 3f;

        private readonly MapData _map;
        private readonly Func<int, EnemyDefinition?> _enemyLookup;
        private readonly Random _random;
        private readonly List<EnemyEntity> _enemies = new List<EnemyEntity>();
        private readonly List<ItemDrop> _drops = new List<ItemDrop>();
        private int _nextInstanceId = 1;
        private int _nextDropId = 1;

        public LocalWorld(MapData map, Func<int, EnemyDefinition?> enemyLookup, Random? random = null, WorldAuthority authority = WorldAuthority.Local)
        {
            _map = map;
            _enemyLookup = enemyLookup;
            _random = random ?? new Random();
            Authority = authority;
        }

        public WorldAuthority Authority { get; }
        public bool IsServerAuthoritative => Authority == WorldAuthority.Server;
        public MapData Map => _map;
        public IReadOnlyList<EnemyEntity> Enemies => _enemies;
        public IReadOnlyList<ItemDrop> Drops => _drops;

        public event Action<EnemyEntity>? EnemyDied;
        public event Action<EnemyEntity>? EnemyRespawned;
        public event Action<ItemDrop>? DropSpawned;

        /// <summary>マップの湧き点すべてから敵を湧かせる(オフライン用)。</summary>
        public void SpawnFromMap()
        {
            foreach (var spawnPoint in _map.SpawnPoints)
            {
                Spawn(spawnPoint);
            }
        }

        public EnemyEntity Spawn(SpawnPoint spawnPoint)
        {
            var enemy = CreateEnemy(_nextInstanceId++, spawnPoint);
            _enemies.Add(enemy);
            return enemy;
        }

        public EnemyEntity? FindEnemy(int instanceId)
        {
            foreach (var enemy in _enemies) if (enemy.InstanceId == instanceId) return enemy;
            return null;
        }

        public ItemDrop? FindDrop(int dropId)
        {
            foreach (var drop in _drops) if (drop.DropId == dropId) return drop;
            return null;
        }

        public void Tick(float dt)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.HitFlash > 0f) enemy.HitFlash -= dt;

                if (IsServerAuthoritative)
                {
                    if (!enemy.IsDead) FollowNetPosition(enemy, dt);
                    continue;
                }

                if (enemy.IsDead)
                {
                    enemy.RespawnTimer -= dt;
                    if (enemy.RespawnTimer <= 0f) Respawn(enemy);
                    continue;
                }

                Patrol(enemy, dt);
            }

            for (var i = _drops.Count - 1; i >= 0; i--)
            {
                _drops[i].RemainingSeconds -= dt;
                // オンラインでは消えるタイミングもサーバーが決める(OnDropRemoved)。届かなかったときの保険に少し長めに残す
                var limit = IsServerAuthoritative ? -10f : 0f;
                if (_drops[i].RemainingSeconds <= limit) _drops.RemoveAt(i);
            }
        }

        /// <summary>
        /// 向いている方向の range 以内で、高さの差が height 以内の生きた敵のうち一番近いもの。
        /// </summary>
        public EnemyEntity? FindAttackTarget(float x, float y, Direction facing, float range, float height)
        {
            EnemyEntity? target = null;
            var bestDistance = float.MaxValue;
            var sign = facing == Direction.Right ? 1f : -1f;

            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead) continue;
                var forward = (enemy.X - x) * sign;
                var halfWidth = enemy.Definition.Width * 0.5f;
                if (forward + halfWidth < 0f || forward - halfWidth > range) continue;
                if (Math.Abs(enemy.Y - y) > height) continue;
                if (forward < bestDistance)
                {
                    target = enemy;
                    bestDistance = forward;
                }
            }
            return target;
        }

        /// <summary>
        /// プレイヤーの攻撃。<see cref="FindAttackTarget"/> の相手に当てる。
        /// オンラインでは被弾の表示だけ行い、Pending の結果を返す(ダメージはサーバーへ送る)。
        /// </summary>
        public AttackOutcome PlayerAttack(float x, float y, Direction facing, float range, float height, int damage)
        {
            var target = FindAttackTarget(x, y, facing, range, height);
            if (target == null) return AttackOutcome.Miss;

            damage = Math.Max(0, damage);
            target.HitFlash = HitFlashSeconds;
            if (IsServerAuthoritative)
            {
                return new AttackOutcome(target, damage, false, Array.Empty<int>(), pending: true);
            }

            target.Hp = Math.Max(0, target.Hp - damage);
            if (target.Hp > 0)
            {
                return new AttackOutcome(target, damage, false, Array.Empty<int>());
            }

            var drops = Kill(target);
            return new AttackOutcome(target, damage, true, drops);
        }

        /// <summary>プレイヤーの当たり判定(足元基準の箱)に重なっている生きた敵。</summary>
        public EnemyEntity? FindTouchingEnemy(float playerX, float playerY, float playerHalfWidth, float playerHeight)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.IsDead) continue;
                var halfWidth = enemy.Definition.Width * 0.5f;
                if (Math.Abs(enemy.X - playerX) > halfWidth + playerHalfWidth) continue;
                if (enemy.Y > playerY + playerHeight || enemy.Y + enemy.Definition.Height < playerY) continue;
                return enemy;
            }
            return null;
        }

        /// <summary>range 以内で最も近いドロップ(取り除かない)。</summary>
        public ItemDrop? FindPickupCandidate(float x, float y, float range)
        {
            ItemDrop? best = null;
            var bestDistance = float.MaxValue;
            foreach (var drop in _drops)
            {
                var dx = Math.Abs(drop.X - x);
                if (dx > range || Math.Abs(drop.Y - y) > 1.5f) continue;
                if (dx < bestDistance)
                {
                    best = drop;
                    bestDistance = dx;
                }
            }
            return best;
        }

        /// <summary>range 以内で最も近いドロップを拾って取り除く(オフライン用)。</summary>
        public ItemDrop? TryPickup(float x, float y, float range)
        {
            var best = FindPickupCandidate(x, y, range);
            if (best != null) _drops.Remove(best);
            return best;
        }

        // ---- オンライン: サーバーの通知を映す ----

        /// <summary>参加時の全状態で置き換える。</summary>
        public void ApplySnapshot(IReadOnlyList<EnemyState> enemies, IReadOnlyList<DropState> drops)
        {
            _enemies.Clear();
            _drops.Clear();
            foreach (var state in enemies) ApplyEnemySpawn(state);
            foreach (var drop in drops) AddDrop(drop);
        }

        /// <summary>敵が湧いた(または復活した)。知らない ID なら新しく作る。</summary>
        public EnemyEntity ApplyEnemySpawn(EnemyState state)
        {
            var enemy = FindEnemy(state.InstanceId);
            var isNew = enemy == null;
            if (enemy == null)
            {
                var spawnPoint = new SpawnPoint { Id = 0, X = state.X, Y = state.Y, EnemyId = state.EnemyId };
                enemy = CreateEnemy(state.InstanceId, spawnPoint);
                _enemies.Add(enemy);
            }

            var wasDead = enemy.IsDead;
            enemy.MaxHp = state.MaxHp > 0 ? state.MaxHp : enemy.Definition.MaxHp;
            enemy.Hp = state.Hp;
            enemy.IsDead = state.IsDead;
            enemy.Facing = ToDirection(state.Facing);
            enemy.X = enemy.NetX = state.X;
            enemy.Y = enemy.NetY = state.Y;
            enemy.RespawnTimer = 0f;
            if (!isNew && wasDead && !enemy.IsDead) EnemyRespawned?.Invoke(enemy);
            return enemy;
        }

        /// <summary>敵の HP が変わった。</summary>
        public EnemyEntity? ApplyEnemyDamaged(int instanceId, int hp)
        {
            var enemy = FindEnemy(instanceId);
            if (enemy == null) return null;
            enemy.Hp = Math.Max(0, hp);
            enemy.HitFlash = HitFlashSeconds;
            return enemy;
        }

        /// <summary>敵が倒れた。</summary>
        public EnemyEntity? ApplyEnemyDead(int instanceId)
        {
            var enemy = FindEnemy(instanceId);
            if (enemy == null || enemy.IsDead) return enemy;
            enemy.Hp = 0;
            enemy.IsDead = true;
            EnemyDied?.Invoke(enemy);
            return enemy;
        }

        /// <summary>敵の位置(定期配信)。表示は Tick で滑らかに寄せる。</summary>
        public void ApplyEnemyMove(IReadOnlyList<EnemyMoveState> moves)
        {
            foreach (var move in moves)
            {
                var enemy = FindEnemy(move.InstanceId);
                if (enemy == null || enemy.IsDead) continue;
                enemy.NetX = move.X;
                enemy.NetY = move.Y;
                enemy.Facing = ToDirection(move.Facing);
            }
        }

        public void ApplyDropSpawn(IReadOnlyList<DropState> drops)
        {
            foreach (var drop in drops) AddDrop(drop);
        }

        /// <summary>落とし物が消えた(誰かが拾った、または時間切れ)。消えたものを返す。</summary>
        public ItemDrop? ApplyDropRemoved(int dropId)
        {
            var drop = FindDrop(dropId);
            if (drop != null) _drops.Remove(drop);
            return drop;
        }

        public static Direction ToDirection(Facing facing) => facing == Facing.Right ? Direction.Right : Direction.Left;

        public static Facing ToFacing(Direction direction) => direction == Direction.Right ? Facing.Right : Facing.Left;

        // ---- 内部 ----

        private EnemyEntity CreateEnemy(int instanceId, SpawnPoint spawnPoint)
        {
            var definition = _enemyLookup(spawnPoint.EnemyId)
                ?? new EnemyDefinition { EnemyId = spawnPoint.EnemyId, Name = $"enemy{spawnPoint.EnemyId}" };
            var ground = _map.FindFootholdBelow(spawnPoint.X, spawnPoint.Y + 0.5f);
            return new EnemyEntity(instanceId, definition, spawnPoint, ground);
        }

        private void AddDrop(DropState state)
        {
            if (FindDrop(state.DropId) != null) return;
            var drop = new ItemDrop(state.DropId, state.ItemId, state.X, state.Y, DropLifetimeSeconds);
            _drops.Add(drop);
            DropSpawned?.Invoke(drop);
        }

        private static void FollowNetPosition(EnemyEntity enemy, float dt)
        {
            var dx = enemy.NetX - enemy.X;
            var dy = enemy.NetY - enemy.Y;
            if (Math.Abs(dx) > NetSnapDistance || Math.Abs(dy) > NetSnapDistance)
            {
                enemy.X = enemy.NetX;
                enemy.Y = enemy.NetY;
                return;
            }

            var t = 1f - (float)Math.Exp(-NetFollowRate * dt);
            enemy.X += dx * t;
            enemy.Y += dy * t;
        }

        private void Patrol(EnemyEntity enemy, float dt)
        {
            var ground = enemy.Ground;
            if (ground == null || ground.Width <= 0f) return;

            var direction = enemy.Facing == Direction.Right ? 1f : -1f;
            var x = enemy.X + direction * enemy.Definition.MoveSpeed * dt;
            if (x >= ground.Right)
            {
                x = ground.Right;
                enemy.Facing = Direction.Left;
            }
            else if (x <= ground.Left)
            {
                x = ground.Left;
                enemy.Facing = Direction.Right;
            }

            enemy.X = x;
            enemy.Y = ground.GetYAt(x);
        }

        private int[] Kill(EnemyEntity enemy)
        {
            enemy.IsDead = true;
            enemy.RespawnTimer = enemy.SpawnPoint.RespawnSeconds;

            var dropped = new List<int>();
            foreach (var itemId in enemy.Definition.DropItemIds)
            {
                if (_random.NextDouble() < enemy.Definition.DropRate) dropped.Add(itemId);
            }

            for (var i = 0; i < dropped.Count; i++)
            {
                var offset = (i - (dropped.Count - 1) * 0.5f) * 0.6f;
                var drop = new ItemDrop(_nextDropId++, dropped[i], enemy.X + offset, enemy.Y, DropLifetimeSeconds);
                _drops.Add(drop);
                DropSpawned?.Invoke(drop);
            }

            EnemyDied?.Invoke(enemy);
            return dropped.ToArray();
        }

        private void Respawn(EnemyEntity enemy)
        {
            enemy.Hp = enemy.MaxHp;
            enemy.IsDead = false;
            enemy.RespawnTimer = 0f;
            enemy.X = enemy.SpawnPoint.X;
            enemy.Y = enemy.Ground?.GetYAt(enemy.X) ?? enemy.SpawnPoint.Y;
            EnemyRespawned?.Invoke(enemy);
        }
    }
}
