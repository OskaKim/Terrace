using System;
using System.Collections.Generic;
using Terrace.Map;

namespace Terrace.Client.Core
{
    /// <summary>
    /// オフラインで動く敵とドロップの世界。サーバー(Terrace.Server の Room)と同じ規則:
    /// 湧き点から湧き、HP を持ち、0 になったら死亡してドロップを抽選し、N 秒後に復活する。
    /// 敵は自分の足場の上を端で折り返しながら巡回する。
    /// </summary>
    public sealed class LocalWorld
    {
        public const float DropLifetimeSeconds = 60f;
        public const float HitFlashSeconds = 0.15f;

        private readonly MapData _map;
        private readonly Func<int, EnemyDefinition?> _enemyLookup;
        private readonly Random _random;
        private readonly List<EnemyEntity> _enemies = new List<EnemyEntity>();
        private readonly List<ItemDrop> _drops = new List<ItemDrop>();
        private int _nextInstanceId = 1;
        private int _nextDropId = 1;

        public LocalWorld(MapData map, Func<int, EnemyDefinition?> enemyLookup, Random? random = null)
        {
            _map = map;
            _enemyLookup = enemyLookup;
            _random = random ?? new Random();
        }

        public IReadOnlyList<EnemyEntity> Enemies => _enemies;
        public IReadOnlyList<ItemDrop> Drops => _drops;

        public event Action<EnemyEntity>? EnemyDied;
        public event Action<EnemyEntity>? EnemyRespawned;
        public event Action<ItemDrop>? DropSpawned;

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
            var definition = _enemyLookup(spawnPoint.EnemyId)
                ?? new EnemyDefinition { EnemyId = spawnPoint.EnemyId, Name = $"enemy{spawnPoint.EnemyId}" };
            var ground = _map.FindFootholdBelow(spawnPoint.X, spawnPoint.Y + 0.5f);
            var enemy = new EnemyEntity(_nextInstanceId++, definition, spawnPoint, ground);
            _enemies.Add(enemy);
            return enemy;
        }

        public EnemyEntity? FindEnemy(int instanceId)
        {
            foreach (var enemy in _enemies) if (enemy.InstanceId == instanceId) return enemy;
            return null;
        }

        public void Tick(float dt)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.HitFlash > 0f) enemy.HitFlash -= dt;

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
                if (_drops[i].RemainingSeconds <= 0f) _drops.RemoveAt(i);
            }
        }

        /// <summary>
        /// プレイヤーの攻撃。向いている方向の range 以内で、高さの差が height 以内の敵のうち一番近いものに当てる。
        /// </summary>
        public AttackOutcome PlayerAttack(float x, float y, Direction facing, float range, float height, int damage)
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

            if (target == null) return AttackOutcome.Miss;

            damage = Math.Max(0, damage);
            target.Hp = Math.Max(0, target.Hp - damage);
            target.HitFlash = HitFlashSeconds;
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

        /// <summary>range 以内で最も近いドロップを拾って取り除く。</summary>
        public ItemDrop? TryPickup(float x, float y, float range)
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

            if (best != null) _drops.Remove(best);
            return best;
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
            enemy.Hp = enemy.Definition.MaxHp;
            enemy.IsDead = false;
            enemy.RespawnTimer = 0f;
            enemy.X = enemy.SpawnPoint.X;
            enemy.Y = enemy.Ground?.GetYAt(enemy.X) ?? enemy.SpawnPoint.Y;
            EnemyRespawned?.Invoke(enemy);
        }
    }
}
