using System.Collections.Generic;
using Terrace.Client.Core;
using Terrace.Map;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>プレイヤーの見た目。シミュレーションの状態を毎フレーム反映する。</summary>
    public sealed class PlayerView : MonoBehaviour
    {
        public const float Width = 0.8f;
        public const float Height = 1.6f;

        private static readonly Color BodyColor = new Color(0.95f, 0.85f, 0.55f);

        private GameSimulation? _simulation;
        private SpriteRenderer? _body;
        private SpriteRenderer? _swing;

        public void Bind(GameSimulation simulation)
        {
            _simulation = simulation;
            _body = SpriteFactory.CreateRenderer("Body", transform, SpriteFactory.RoundedRect(), BodyColor, Width, Height, 10);
            var eye = SpriteFactory.CreateRenderer("Eye", _body.transform, SpriteFactory.Square(), new Color(0.15f, 0.15f, 0.2f), 0.14f, 0.12f, 11);
            eye.transform.localPosition = new Vector3(0.22f, 0.72f, 0f);
            _swing = SpriteFactory.CreateRenderer("Swing", transform, SpriteFactory.Circle(), new Color(1f, 1f, 1f, 0.6f), 1.4f, 1.0f, 9);
            _swing.transform.localPosition = new Vector3(1.0f, 0.3f, 0f);
            _swing.enabled = false;
            Sync();
        }

        public void Sync()
        {
            if (_simulation == null || _body == null || _swing == null) return;
            var motor = _simulation.Motor;
            var player = _simulation.Player;

            transform.position = new Vector3(motor.X, motor.Y, 0f);
            var facing = motor.Facing == Direction.Right ? 1f : -1f;
            transform.localScale = new Vector3(facing, 1f, 1f);
            _body.transform.localScale = new Vector3(Width, motor.IsCrouching ? Height * 0.65f : Height, 1f);

            var color = BodyColor;
            if (player.IsDead) color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            else if (player.IsInvulnerable && Mathf.FloorToInt(Time.time * 12f) % 2 == 0) color = new Color(1f, 0.6f, 0.6f, 0.7f);
            else if (motor.Mode == MotorMode.Ladder) color = new Color(0.85f, 0.85f, 0.65f);
            _body.color = color;

            _swing.enabled = !player.IsDead && motor.IsAttackLocked;
        }
    }

    /// <summary>敵 1 体の見た目。</summary>
    public sealed class EnemyView : MonoBehaviour
    {
        private EnemyEntity? _enemy;
        private SpriteRenderer? _body;
        private Color _baseColor;

        public EnemyEntity? Enemy => _enemy;

        public void Bind(EnemyEntity enemy)
        {
            _enemy = enemy;
            var (sprite, color, width, height) = Look(enemy.Definition);
            _baseColor = color;
            _body = SpriteFactory.CreateRenderer("Body", transform, sprite, color, width, height, 8);
            var eye = SpriteFactory.CreateRenderer("Eye", _body.transform, SpriteFactory.Square(), new Color(0.1f, 0.1f, 0.1f), 0.12f, 0.12f, 9);
            eye.transform.localPosition = new Vector3(-0.2f, 0.55f, 0f);
            Sync();
        }

        public void Sync()
        {
            if (_enemy == null || _body == null) return;
            gameObject.SetActive(_enemy.IsAlive);
            if (!_enemy.IsAlive) return;

            transform.position = new Vector3(_enemy.X, _enemy.Y, 0f);
            transform.localScale = new Vector3(_enemy.Facing == Direction.Right ? -1f : 1f, 1f, 1f);
            _body.color = _enemy.HitFlash > 0f ? Color.white : _baseColor;
        }

        private static (Sprite, Color, float, float) Look(EnemyDefinition definition)
        {
            switch (definition.EnemyId)
            {
                case 1: return (SpriteFactory.Circle(), new Color(0.40f, 0.85f, 0.45f), 1.0f, 0.8f);
                case 2: return (SpriteFactory.RoundedRect(), new Color(0.85f, 0.35f, 0.30f), 0.9f, 1.3f);
                case 3: return (SpriteFactory.Circle(), new Color(0.75f, 0.55f, 0.95f, 0.85f), 0.9f, 1.1f);
                default: return (SpriteFactory.RoundedRect(), new Color(0.6f, 0.6f, 0.6f), definition.Width, definition.Height);
            }
        }
    }

    /// <summary>落ちているアイテム。</summary>
    public sealed class DropView : MonoBehaviour
    {
        private ItemDrop? _drop;
        private float _phase;

        public ItemDrop? Drop => _drop;

        public void Bind(ItemDrop drop)
        {
            _drop = drop;
            _phase = drop.DropId * 0.7f;
            SpriteFactory.CreateRenderer("Item", transform, SpriteFactory.Diamond(), new Color(1f, 0.85f, 0.25f), 0.45f, 0.45f, 7);
            Sync();
        }

        public void Sync()
        {
            if (_drop == null) return;
            var bob = Mathf.Sin(Time.time * 4f + _phase) * 0.08f + 0.1f;
            transform.position = new Vector3(_drop.X, _drop.Y + bob, 0f);
        }
    }

    /// <summary>敵とドロップの見た目をシミュレーションと同期させる。</summary>
    public sealed class WorldViewSync
    {
        private readonly Transform _root;
        private readonly List<EnemyView> _enemies = new List<EnemyView>();
        private readonly Dictionary<int, DropView> _drops = new Dictionary<int, DropView>();
        private readonly List<int> _removedDrops = new List<int>();

        public WorldViewSync(Transform root, LocalWorld world)
        {
            _root = root;
            foreach (var enemy in world.Enemies)
            {
                var go = new GameObject($"Enemy {enemy.Definition.Name}#{enemy.InstanceId}");
                go.transform.SetParent(root, false);
                var view = go.AddComponent<EnemyView>();
                view.Bind(enemy);
                _enemies.Add(view);
            }
        }

        public IReadOnlyList<EnemyView> Enemies => _enemies;

        public void Sync(LocalWorld world)
        {
            foreach (var view in _enemies) view.Sync();

            foreach (var drop in world.Drops)
            {
                if (_drops.ContainsKey(drop.DropId)) continue;
                var go = new GameObject($"Drop item{drop.ItemId}#{drop.DropId}");
                go.transform.SetParent(_root, false);
                var view = go.AddComponent<DropView>();
                view.Bind(drop);
                _drops[drop.DropId] = view;
            }

            _removedDrops.Clear();
            foreach (var pair in _drops)
            {
                var stillExists = false;
                foreach (var drop in world.Drops)
                {
                    if (drop.DropId == pair.Key)
                    {
                        stillExists = true;
                        break;
                    }
                }
                if (!stillExists) _removedDrops.Add(pair.Key);
                else pair.Value.Sync();
            }
            foreach (var id in _removedDrops)
            {
                Object.Destroy(_drops[id].gameObject);
                _drops.Remove(id);
            }
        }
    }
}
