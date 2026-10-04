using System;

namespace Terrace.Client.Core
{
    /// <summary>
    /// 攻撃の係: 向いている方向の一番近い敵を探して当て、世界の権威へダメージを頼む。
    /// HP・撃破は権威の結果のイベントで分かる(報酬は KillRewardSystem)。規則は docs/spec/combat.md。
    /// </summary>
    public sealed class CombatSystem
    {
        private readonly GameContext _context;

        public CombatSystem(GameContext context)
        {
            _context = context;
            context.EnemyDamaged += OnEnemyDamaged;
            context.EnemyKilled += OnEnemyKilled;
        }

        /// <summary>攻撃を振った(当たったかどうか。外れなら AttackOutcome.Miss)。HP を減らすより前に起きる。</summary>
        public event Action<AttackOutcome>? Attacked;

        /// <summary>攻撃を振る(攻撃の動作が始まったとき)。</summary>
        public void Attack()
        {
            var motor = _context.Motor;
            var config = _context.PlayerConfig;
            var target = _context.World.FindAttackTarget(motor.X, motor.Y, motor.Facing, config.AttackRange, config.AttackHeight);
            if (target == null)
            {
                Attacked?.Invoke(AttackOutcome.Miss);
                return;
            }

            var damage = Math.Max(0, _context.Player.Attack);
            Attacked?.Invoke(new AttackOutcome(target, damage));
            _context.Authority.RequestAttack(target, damage);
        }

        private void OnEnemyDamaged(EnemyDamage damage)
        {
            if (!damage.Attacker.IsSelf || damage.Enemy.Hp <= 0) return;
            _context.Messages.Add(_context.Time, $"{damage.Enemy.Definition.Name} に {damage.Damage} ダメージ (残り {damage.Enemy.Hp})");
        }

        private void OnEnemyKilled(EnemyKill kill)
        {
            if (kill.Killer.IsSelf) return;
            var killerId = kill.Killer.PlayerId;
            var killer = _context.RemotePlayers.Find(killerId)?.Name ?? $"player{killerId}";
            _context.Messages.Add(_context.Time, $"{killer} が {kill.Enemy.Definition.Name} を倒した");
        }
    }
}
