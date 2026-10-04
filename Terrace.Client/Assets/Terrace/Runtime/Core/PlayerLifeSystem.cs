using System;

namespace Terrace.Client.Core
{
    /// <summary>
    /// プレイヤーの生死の係: 敵との接触による被弾・ノックバック・無敵時間・死亡(敵・落下)・復活待ちと復活。
    /// 規則は docs/spec/combat.md。
    /// </summary>
    public sealed class PlayerLifeSystem
    {
        private readonly GameContext _context;

        public PlayerLifeSystem(GameContext context)
        {
            _context = context;
        }

        /// <summary>倒れる直前(店を閉じるなど、倒れる前に片付けたい係のため)。</summary>
        public event Action? Dying;

        /// <summary>敵に触れて被弾した。引数は触れた敵とダメージ。</summary>
        public event Action<EnemyEntity, int>? PlayerDamaged;

        /// <summary>倒れた。引数は死因(敵の名前・落下)。</summary>
        public event Action<string>? PlayerDied;

        public event Action? PlayerRespawned;

        /// <summary>
        /// 時間を進める。倒れている間は復活を待ち(時間が来たら復活する)、生きていれば無敵時間を減らす。
        /// このフレームにプレイヤーが動けるなら true(倒れている間は false)。
        /// </summary>
        public bool Tick(float dt)
        {
            var player = _context.Player;
            if (player.IsDead)
            {
                player.RespawnTimer -= dt;
                if (player.RespawnTimer <= 0f) Respawn();
                return false;
            }

            if (player.InvulnerableTimer > 0f) player.InvulnerableTimer -= dt;
            return true;
        }

        /// <summary>無敵中でもはしごの上でもなければ、触れている敵から被弾する。</summary>
        public void CheckContact()
        {
            var player = _context.Player;
            var motor = _context.Motor;
            var config = _context.PlayerConfig;
            if (player.IsInvulnerable || motor.Mode == MotorMode.Ladder) return;

            var touching = _context.World.FindTouchingEnemy(motor.X, motor.Y, config.HalfWidth, config.Height);
            if (touching != null) TakeContactDamage(touching);
        }

        /// <summary>倒れる。cause はメッセージに出す死因(敵の名前・落下)。</summary>
        public void Kill(string cause)
        {
            Dying?.Invoke();
            var player = _context.Player;
            player.Hp = 0;
            player.IsDead = true;
            player.RespawnTimer = _context.PlayerConfig.RespawnSeconds;
            PlayerDied?.Invoke(cause);
        }

        private void TakeContactDamage(EnemyEntity enemy)
        {
            var player = _context.Player;
            var config = _context.PlayerConfig;
            var damage = Math.Max(1, enemy.Definition.Attack);
            player.Hp = Math.Max(0, player.Hp - damage);
            player.InvulnerableTimer = config.InvulnerableSeconds;
            PlayerDamaged?.Invoke(enemy, damage);

            var motor = _context.Motor;
            var away = motor.X >= enemy.X ? 1f : -1f;
            motor.Launch(away * config.KnockbackVelocityX, config.KnockbackVelocityY);

            if (player.Hp <= 0) Kill(enemy.Definition.Name);
        }

        private void Respawn()
        {
            var player = _context.Player;
            player.Hp = player.MaxHp;
            player.IsDead = false;
            player.RespawnTimer = 0f;
            player.InvulnerableTimer = _context.PlayerConfig.InvulnerableSeconds;
            _context.Motor.Teleport(_context.SpawnPosition.X, _context.SpawnPosition.Y);
            PlayerRespawned?.Invoke();
        }
    }
}
