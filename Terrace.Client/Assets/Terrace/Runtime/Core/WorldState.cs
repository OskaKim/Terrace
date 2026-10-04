using System;
using System.Collections.Generic;
using Terrace.Map;

namespace Terrace.Client.Core
{
    /// <summary>
    /// 1 マップ分の敵と落とし物の入れ物と、その問い合わせ(攻撃の相手探し・接触する敵・拾える落とし物)。
    /// 誰が状態を決めるかは知らない。決めるのは世界の権威(<see cref="IWorldAuthority"/>)で、中身を書き換えるのも権威だけ。
    /// 見た目と HUD はここを読む。
    /// </summary>
    public sealed class WorldState
    {
        /// <summary>落とし物が消えるまでの秒数(Server の Room と同じ規則)。</summary>
        public const float DropLifetimeSeconds = 60f;

        /// <summary>被弾表示の長さ。</summary>
        public const float HitFlashSeconds = 0.15f;

        private readonly List<EnemyEntity> _enemies = new List<EnemyEntity>();
        private readonly List<ItemDrop> _drops = new List<ItemDrop>();

        public WorldState(MapData map)
        {
            Map = map;
        }

        public MapData Map { get; }
        public IReadOnlyList<EnemyEntity> Enemies => _enemies;
        public IReadOnlyList<ItemDrop> Drops => _drops;

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

        // ---- 権威だけが使う書き換え ----

        internal void AddEnemy(EnemyEntity enemy) => _enemies.Add(enemy);

        internal void AddDrop(ItemDrop drop) => _drops.Add(drop);

        internal bool RemoveDrop(ItemDrop drop) => _drops.Remove(drop);

        internal void Clear()
        {
            _enemies.Clear();
            _drops.Clear();
        }

        /// <summary>被弾表示の残りを減らす。</summary>
        internal void TickHitFlash(float dt)
        {
            foreach (var enemy in _enemies)
            {
                if (enemy.HitFlash > 0f) enemy.HitFlash -= dt;
            }
        }

        /// <summary>落とし物の残り時間を減らし、-grace 秒を過ぎたものを取り除いて expired に足す。</summary>
        internal void ExpireDrops(float dt, float grace, List<ItemDrop> expired)
        {
            for (var i = _drops.Count - 1; i >= 0; i--)
            {
                _drops[i].RemainingSeconds -= dt;
                if (_drops[i].RemainingSeconds > -grace) continue;
                expired.Add(_drops[i]);
                _drops.RemoveAt(i);
            }
        }
    }
}
