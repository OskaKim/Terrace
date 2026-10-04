using System;
using System.Collections.Generic;
using Terrace.Map;
using Terrace.Shared;

namespace Terrace.Client.Core.Online
{
    /// <summary>
    /// オンラインの世界の権威。敵と落とし物の状態はサーバー(Terrace.Server の Room)が決め、ここは届いた結果を映すだけ。
    /// 自分では湧かせず、動かさず、HP を減らさない。
    ///
    /// 攻撃と拾うは送り口(<see cref="IOnlineChannel"/>)で送るだけで、結果のイベントはサーバーの通知を Apply* で映したときに出す。
    /// 攻撃した敵の被弾表示だけは先に出す。敵の表示位置は、届いた位置へ毎フレーム滑らかに寄せる。
    /// マップに入るたびに新しく作る(前のマップの状態は持ち越さない)。
    /// </summary>
    public sealed class RoomMirror : IWorldAuthority
    {
        /// <summary>表示位置をサーバー位置へ寄せる速さ(1 秒あたりの追従率)。</summary>
        public const float NetFollowRate = 10f;

        /// <summary>これ以上離れていたら寄せずに瞬間移動する。</summary>
        public const float NetSnapDistance = 3f;

        /// <summary>落とし物は消える時間をサーバーが決める(消えた通知)。届かなかったときの保険に、寿命からこれだけ長く残す。</summary>
        private const float DropExpiryGraceSeconds = 10f;

        private readonly Func<int, EnemyDefinition?> _enemyLookup;
        private readonly IOnlineChannel _channel;
        private readonly Dictionary<int, (float X, float Y)> _netPositions = new Dictionary<int, (float X, float Y)>();
        private readonly HashSet<int> _pendingPickups = new HashSet<int>();
        private readonly List<ItemDrop> _expired = new List<ItemDrop>();

        public RoomMirror(MapData map, Func<int, EnemyDefinition?> enemyLookup, IOnlineChannel channel)
        {
            _enemyLookup = enemyLookup;
            _channel = channel;
            State = new WorldState(map);
        }

        public WorldState State { get; }

        public event Action<EnemyDamage>? EnemyDamaged;
        public event Action<EnemyKill>? EnemyKilled;
        public event Action<EnemyEntity>? EnemySpawned;
        public event Action<ItemDrop>? DropSpawned;
        public event Action<DropRemoval>? DropRemoved;

        public void Tick(float dt)
        {
            State.TickHitFlash(dt);
            foreach (var enemy in State.Enemies)
            {
                if (!enemy.IsDead) FollowNetPosition(enemy, dt);
            }

            _expired.Clear();
            State.ExpireDrops(dt, DropExpiryGraceSeconds, _expired);
            foreach (var drop in _expired)
            {
                _pendingPickups.Remove(drop.DropId);
                DropRemoved?.Invoke(new DropRemoval(drop, WorldActor.None));
            }
        }

        /// <summary>当たった敵とダメージを送り、被弾表示だけ先に出す。HP と撃破はサーバーの通知で反映する。</summary>
        public void RequestAttack(EnemyEntity target, int damage)
        {
            if (target.IsDead) return;
            target.HitFlash = WorldState.HitFlashSeconds;
            _channel.Attack(target.InstanceId, Math.Max(0, damage));
        }

        /// <summary>拾いたいと送る。返事(消えた通知)が来るまで、同じ落とし物は重ねて送らない。</summary>
        public void RequestPickup(ItemDrop drop)
        {
            if (State.FindDrop(drop.DropId) == null || !_pendingPickups.Add(drop.DropId)) return;
            _channel.Pickup(drop.DropId);
        }

        // ---- サーバーの通知を映す ----

        /// <summary>参加時の全状態で置き換える。全部の入れ替えなので、湧いた・出たのイベントは出さない。</summary>
        public void ApplySnapshot(IReadOnlyList<EnemyState> enemies, IReadOnlyList<DropState> drops)
        {
            State.Clear();
            _netPositions.Clear();
            _pendingPickups.Clear();
            foreach (var state in enemies) Upsert(state);
            foreach (var drop in drops) AddDrop(drop);
        }

        /// <summary>敵が湧いた(または復活した)。知らない ID なら新しく作る。</summary>
        public EnemyEntity ApplyEnemySpawn(EnemyState state)
        {
            var existing = State.FindEnemy(state.InstanceId);
            var wasKnown = existing != null;
            var wasDead = existing?.IsDead ?? true;
            var enemy = Upsert(state);
            if (!enemy.IsDead && (!wasKnown || wasDead)) EnemySpawned?.Invoke(enemy);
            return enemy;
        }

        /// <summary>敵の HP が変わった。</summary>
        public EnemyEntity? ApplyEnemyDamaged(int instanceId, int hp, int attackerPlayerId, int damage)
        {
            var enemy = State.FindEnemy(instanceId);
            if (enemy == null) return null;
            enemy.Hp = Math.Max(0, hp);
            enemy.HitFlash = WorldState.HitFlashSeconds;
            EnemyDamaged?.Invoke(new EnemyDamage(enemy, damage, ActorOf(attackerPlayerId)));
            return enemy;
        }

        /// <summary>敵が倒れた。すでに倒れている敵への通知(重なって届いたもの)では、何も起きない。</summary>
        public EnemyEntity? ApplyEnemyDead(int instanceId, int killerPlayerId, int[] droppedItemIds)
        {
            var enemy = State.FindEnemy(instanceId);
            if (enemy == null || enemy.IsDead) return enemy;
            enemy.Hp = 0;
            enemy.IsDead = true;
            EnemyKilled?.Invoke(new EnemyKill(enemy, ActorOf(killerPlayerId), droppedItemIds ?? Array.Empty<int>()));
            return enemy;
        }

        /// <summary>敵の位置(定期配信)。表示は Tick で滑らかに寄せる。</summary>
        public void ApplyEnemyMove(IReadOnlyList<EnemyMoveState> moves)
        {
            foreach (var move in moves)
            {
                var enemy = State.FindEnemy(move.InstanceId);
                if (enemy == null || enemy.IsDead) continue;
                _netPositions[enemy.InstanceId] = (move.X, move.Y);
                enemy.Facing = ToDirection(move.Facing);
            }
        }

        public void ApplyDropSpawn(IReadOnlyList<DropState> drops)
        {
            foreach (var state in drops)
            {
                var drop = AddDrop(state);
                if (drop != null) DropSpawned?.Invoke(drop);
            }
        }

        /// <summary>落とし物が消えた(誰かが拾った、または時間切れ)。消えたものを返す。</summary>
        public ItemDrop? ApplyDropRemoved(int dropId, int playerId)
        {
            _pendingPickups.Remove(dropId);
            var drop = State.FindDrop(dropId);
            if (drop == null) return null;
            State.RemoveDrop(drop);
            DropRemoved?.Invoke(new DropRemoval(drop, ActorOf(playerId)));
            return drop;
        }

        public static Direction ToDirection(Facing facing) => facing == Facing.Right ? Direction.Right : Direction.Left;

        public static Facing ToFacing(Direction direction) => direction == Direction.Right ? Facing.Right : Facing.Left;

        // ---- 内部 ----

        private WorldActor ActorOf(int playerId)
        {
            if (playerId == 0) return WorldActor.None;
            return playerId == _channel.Self.PlayerId ? WorldActor.Self : WorldActor.Other(playerId);
        }

        private EnemyEntity Upsert(EnemyState state)
        {
            var enemy = State.FindEnemy(state.InstanceId);
            if (enemy == null)
            {
                enemy = new EnemyEntity(state.InstanceId, EnemyDefinition.Resolve(_enemyLookup, state.EnemyId), state.X, state.Y);
                State.AddEnemy(enemy);
            }

            enemy.MaxHp = state.MaxHp > 0 ? state.MaxHp : enemy.Definition.MaxHp;
            enemy.Hp = state.Hp;
            enemy.IsDead = state.IsDead;
            enemy.Facing = ToDirection(state.Facing);
            enemy.X = state.X;
            enemy.Y = state.Y;
            _netPositions[enemy.InstanceId] = (state.X, state.Y);
            return enemy;
        }

        private ItemDrop? AddDrop(DropState state)
        {
            if (State.FindDrop(state.DropId) != null) return null;
            var drop = new ItemDrop(state.DropId, state.ItemId, state.X, state.Y, WorldState.DropLifetimeSeconds);
            State.AddDrop(drop);
            return drop;
        }

        private void FollowNetPosition(EnemyEntity enemy, float dt)
        {
            if (!_netPositions.TryGetValue(enemy.InstanceId, out var net)) return;
            var dx = net.X - enemy.X;
            var dy = net.Y - enemy.Y;
            if (Math.Abs(dx) > NetSnapDistance || Math.Abs(dy) > NetSnapDistance)
            {
                enemy.X = net.X;
                enemy.Y = net.Y;
                return;
            }

            var t = 1f - (float)Math.Exp(-NetFollowRate * dt);
            enemy.X += dx * t;
            enemy.Y += dy * t;
        }
    }
}
