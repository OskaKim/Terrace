using System;
using Terrace.Map;

namespace Terrace.Client.Core
{
    /// <summary>
    /// ゲーム 1 セッション分のシミュレーション(オフライン)。
    /// 入力を受けて、移動・攻撃・接触ダメージ・拾う・ポータル・死亡と復活を進める。UnityEngine には依存しない。
    /// </summary>
    public sealed class GameSimulation
    {
        private readonly Func<int, string> _itemNameLookup;

        public GameSimulation(
            MapData map,
            Func<int, EnemyDefinition?> enemyLookup,
            Func<int, string> itemNameLookup,
            MotorConfig? motorConfig = null,
            PlayerConfig? playerConfig = null,
            Random? random = null)
        {
            Map = map;
            _itemNameLookup = itemNameLookup;
            PlayerConfig = playerConfig ?? PlayerConfig.Default;
            Player = new PlayerState(PlayerConfig.MaxHp);
            Messages = new MessageLog();
            SpawnPosition = ResolveSpawnPosition(map);
            Motor = new CharacterMotor(map, motorConfig ?? MotorConfig.Default, SpawnPosition.X, SpawnPosition.Y);
            World = new LocalWorld(map, enemyLookup, random);
            World.SpawnFromMap();
        }

        public MapData Map { get; }
        public CharacterMotor Motor { get; }
        public LocalWorld World { get; }
        public PlayerState Player { get; }
        public PlayerConfig PlayerConfig { get; }
        public MessageLog Messages { get; }
        public Position SpawnPosition { get; }
        public float Time { get; private set; }

        public event Action<AttackOutcome>? Attacked;
        public event Action<int>? PlayerDamaged;
        public event Action? PlayerDied;
        public event Action? PlayerRespawned;
        public event Action<ItemDrop>? ItemPickedUp;
        public event Action<Portal, Portal?>? PortalUsed;

        public void Step(in InputFrame input, float dt)
        {
            Time += dt;
            World.Tick(dt);

            if (Player.IsDead)
            {
                Player.RespawnTimer -= dt;
                if (Player.RespawnTimer <= 0f) RespawnPlayer();
                return;
            }

            if (Player.InvulnerableTimer > 0f) Player.InvulnerableTimer -= dt;

            var events = Motor.Step(input, dt);

            if (events.AttackStarted)
            {
                ResolveAttack();
            }

            if (events.EnteredPortal != null)
            {
                EnterPortal(events.EnteredPortal);
            }

            if (events.FellOutOfWorld)
            {
                KillPlayer("落下");
                return;
            }

            if (input.PickupPressed)
            {
                var drop = World.TryPickup(Motor.X, Motor.Y, PlayerConfig.PickupRange);
                if (drop != null)
                {
                    Player.Inventory.Add(drop.ItemId);
                    Messages.Add(Time, $"{ItemName(drop.ItemId)} を拾った");
                    ItemPickedUp?.Invoke(drop);
                }
            }

            if (!Player.IsInvulnerable && Motor.Mode != MotorMode.Ladder)
            {
                var touching = World.FindTouchingEnemy(Motor.X, Motor.Y, PlayerConfig.HalfWidth, PlayerConfig.Height);
                if (touching != null) TakeContactDamage(touching);
            }
        }

        public string ItemName(int itemId) => _itemNameLookup(itemId);

        private void ResolveAttack()
        {
            var outcome = World.PlayerAttack(Motor.X, Motor.Y, Motor.Facing, PlayerConfig.AttackRange, PlayerConfig.AttackHeight, PlayerConfig.AttackDamage);
            Attacked?.Invoke(outcome);
            if (!outcome.Hit) return;

            var name = outcome.Target!.Definition.Name;
            if (outcome.Killed)
            {
                Player.Kills++;
                var drops = outcome.DroppedItemIds.Length == 0
                    ? ""
                    : $" ドロップ: {string.Join(", ", Array.ConvertAll(outcome.DroppedItemIds, ItemName))}";
                Messages.Add(Time, $"{name} を倒した{drops}");
            }
            else
            {
                Messages.Add(Time, $"{name} に {outcome.Damage} ダメージ (残り {outcome.Target.Hp})");
            }
        }

        private void TakeContactDamage(EnemyEntity enemy)
        {
            var damage = Math.Max(1, enemy.Definition.Attack);
            Player.Hp = Math.Max(0, Player.Hp - damage);
            Player.InvulnerableTimer = PlayerConfig.InvulnerableSeconds;
            Messages.Add(Time, $"{enemy.Definition.Name} から {damage} ダメージ");
            PlayerDamaged?.Invoke(damage);

            var away = Motor.X >= enemy.X ? 1f : -1f;
            Motor.Launch(away * PlayerConfig.KnockbackVelocityX, PlayerConfig.KnockbackVelocityY);

            if (Player.Hp <= 0) KillPlayer(enemy.Definition.Name);
        }

        private void KillPlayer(string cause)
        {
            Player.Hp = 0;
            Player.IsDead = true;
            Player.RespawnTimer = PlayerConfig.RespawnSeconds;
            Messages.Add(Time, $"倒れた… ({cause})");
            PlayerDied?.Invoke();
        }

        private void RespawnPlayer()
        {
            Player.Hp = Player.MaxHp;
            Player.IsDead = false;
            Player.RespawnTimer = 0f;
            Player.InvulnerableTimer = PlayerConfig.InvulnerableSeconds;
            Motor.Teleport(SpawnPosition.X, SpawnPosition.Y);
            Messages.Add(Time, "復活した");
            PlayerRespawned?.Invoke();
        }

        private void EnterPortal(Portal portal)
        {
            if (portal.TargetMapId == Map.Id)
            {
                var target = Map.FindPortalByName(portal.TargetPortalName);
                if (target != null)
                {
                    Motor.Teleport(target.X, target.Y);
                    Messages.Add(Time, $"ポータル {portal.Name} から {target.Name} へ移動");
                    PortalUsed?.Invoke(portal, target);
                    return;
                }
            }

            Messages.Add(Time, $"ポータル {portal.Name} の行き先 (map {portal.TargetMapId} '{portal.TargetPortalName}') はまだありません");
            PortalUsed?.Invoke(portal, null);
        }

        private static Position ResolveSpawnPosition(MapData map)
        {
            var spawn = map.FindPortalByName("spawn");
            if (spawn != null) return new Position(spawn.X, spawn.Y);
            if (map.Portals.Count > 0) return map.Portals[0].Position;
            if (map.Footholds.Count > 0)
            {
                var first = map.Footholds[0];
                var x = (first.Left + first.Right) * 0.5f;
                return new Position(x, first.GetYAt(x));
            }
            return Position.Zero;
        }
    }
}
