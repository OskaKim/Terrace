using System;
using System.Collections.Generic;
using Terrace.Map;

namespace Terrace.Client.Core
{
    /// <summary>
    /// オフラインの世界の権威。Server の Room(Terrace.Server/src/Terrace.Server/Rooms/Room.cs)と同じ規則で、
    /// 敵と落とし物の状態をこのクライアントが決める。規則の正は docs/spec/enemy-drop.md。
    ///
    /// 湧き点から湧き、HP を持ち、0 になったら死亡してドロップを抽選し、湧き点の秒数の後に復活する。
    /// 敵は足場の上を端で折り返しながら巡回する。落とし物は一定時間で消える。
    /// 攻撃と拾うは頼まれたその場で決め、すぐ結果のイベントを出す(した人は常に自分)。
    /// </summary>
    public sealed class OfflineRoom : IWorldAuthority
    {
        private readonly MapData _map;
        private readonly Func<int, EnemyDefinition?> _enemyLookup;
        private readonly Random _random;
        private readonly Dictionary<EnemyEntity, SpawnedEnemy> _spawned = new Dictionary<EnemyEntity, SpawnedEnemy>();
        private readonly List<ItemDrop> _expired = new List<ItemDrop>();
        private int _nextInstanceId = 1;
        private int _nextDropId = 1;

        /// <summary>オフラインでだけ要る敵の値(湧き点・立っている足場・復活待ち)。</summary>
        private sealed class SpawnedEnemy
        {
            public SpawnedEnemy(SpawnPoint spawnPoint, Foothold? ground)
            {
                SpawnPoint = spawnPoint;
                Ground = ground;
            }

            public SpawnPoint SpawnPoint { get; }
            public Foothold? Ground { get; }
            public float RespawnTimer { get; set; }
        }

        public OfflineRoom(MapData map, Func<int, EnemyDefinition?> enemyLookup, Random? random = null)
        {
            _map = map;
            _enemyLookup = enemyLookup;
            _random = random ?? new Random();
            State = new WorldState(map);
        }

        public WorldState State { get; }

        public event Action<EnemyDamage>? EnemyDamaged;
        public event Action<EnemyKill>? EnemyKilled;
        public event Action<EnemyEntity>? EnemySpawned;
        public event Action<ItemDrop>? DropSpawned;
        public event Action<DropRemoval>? DropRemoved;

        /// <summary>マップの湧き点すべてから敵を湧かせる。</summary>
        public void SpawnFromMap()
        {
            foreach (var spawnPoint in _map.SpawnPoints)
            {
                Spawn(spawnPoint);
            }
        }

        public EnemyEntity Spawn(SpawnPoint spawnPoint)
        {
            var definition = EnemyDefinition.Resolve(_enemyLookup, spawnPoint.EnemyId);
            var ground = _map.FindFootholdBelow(spawnPoint.X, spawnPoint.Y + 0.5f);
            var enemy = new EnemyEntity(_nextInstanceId++, definition, spawnPoint.X, ground?.GetYAt(spawnPoint.X) ?? spawnPoint.Y);
            State.AddEnemy(enemy);
            _spawned[enemy] = new SpawnedEnemy(spawnPoint, ground);
            EnemySpawned?.Invoke(enemy);
            return enemy;
        }

        /// <summary>敵が立って巡回する足場(試験と調べもの用)。</summary>
        internal Foothold? GroundOf(EnemyEntity enemy) => _spawned.TryGetValue(enemy, out var spawned) ? spawned.Ground : null;

        public void Tick(float dt)
        {
            State.TickHitFlash(dt);
            foreach (var enemy in State.Enemies)
            {
                var spawned = _spawned[enemy];
                if (enemy.IsDead)
                {
                    spawned.RespawnTimer -= dt;
                    if (spawned.RespawnTimer <= 0f) Respawn(enemy, spawned);
                    continue;
                }

                Patrol(enemy, spawned, dt);
            }

            _expired.Clear();
            State.ExpireDrops(dt, 0f, _expired);
            foreach (var drop in _expired) DropRemoved?.Invoke(new DropRemoval(drop, WorldActor.None));
        }

        /// <summary>その場で HP を減らし、0 になれば倒してドロップを抽選する。</summary>
        public void RequestAttack(EnemyEntity target, int damage)
        {
            if (target.IsDead || !_spawned.TryGetValue(target, out var spawned)) return;

            damage = Math.Max(0, damage);
            target.HitFlash = WorldState.HitFlashSeconds;
            target.Hp = Math.Max(0, target.Hp - damage);
            EnemyDamaged?.Invoke(new EnemyDamage(target, damage, WorldActor.Self));
            if (target.Hp > 0) return;

            var drops = Kill(target, spawned);
            EnemyKilled?.Invoke(new EnemyKill(target, WorldActor.Self, drops));
        }

        /// <summary>その場で拾って取り除く。</summary>
        public void RequestPickup(ItemDrop drop)
        {
            if (!State.RemoveDrop(drop)) return;
            DropRemoved?.Invoke(new DropRemoval(drop, WorldActor.Self));
        }

        private void Patrol(EnemyEntity enemy, SpawnedEnemy spawned, float dt)
        {
            var ground = spawned.Ground;
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

        private int[] Kill(EnemyEntity enemy, SpawnedEnemy spawned)
        {
            enemy.IsDead = true;
            spawned.RespawnTimer = spawned.SpawnPoint.RespawnSeconds;

            var dropped = new List<int>();
            foreach (var itemId in enemy.Definition.DropItemIds)
            {
                if (_random.NextDouble() < enemy.Definition.DropRate) dropped.Add(itemId);
            }

            for (var i = 0; i < dropped.Count; i++)
            {
                var offset = (i - (dropped.Count - 1) * 0.5f) * 0.6f;
                var drop = new ItemDrop(_nextDropId++, dropped[i], enemy.X + offset, enemy.Y, WorldState.DropLifetimeSeconds);
                State.AddDrop(drop);
                DropSpawned?.Invoke(drop);
            }

            return dropped.ToArray();
        }

        private void Respawn(EnemyEntity enemy, SpawnedEnemy spawned)
        {
            enemy.Hp = enemy.MaxHp;
            enemy.IsDead = false;
            spawned.RespawnTimer = 0f;
            enemy.X = spawned.SpawnPoint.X;
            enemy.Y = spawned.Ground?.GetYAt(enemy.X) ?? spawned.SpawnPoint.Y;
            EnemySpawned?.Invoke(enemy);
        }
    }
}
