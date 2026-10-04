using System.Collections.Generic;
using Terrace.Client.Core;
using Terrace.Map;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>プレイヤーの見た目。素材があれば Kenney の絵をこま送りし、無ければコード生成スプライト。</summary>
    public sealed class PlayerView : MonoBehaviour
    {
        public const float FallbackWidth = 0.8f;
        public const float FallbackHeight = 1.6f;

        private static readonly Color FallbackBodyColor = new Color(0.95f, 0.85f, 0.55f);

        private GameSimulation? _simulation;
        private CharacterArt? _art;
        private SpriteRenderer? _body;
        private SpriteAnimator? _animator;
        private SpriteRenderer? _swing;
        private Color _baseColor = FallbackBodyColor;
        private float _lastY;

        public void Bind(GameSimulation simulation, ArtLibrary? art)
        {
            _simulation = simulation;
            _art = art?.Player;

            if (_art != null)
            {
                var bodyGo = new GameObject("Body");
                bodyGo.transform.SetParent(transform, false);
                _body = bodyGo.AddComponent<SpriteRenderer>();
                _body.sprite = _art.Stand;
                _body.sortingOrder = 10;
                _animator = bodyGo.AddComponent<SpriteAnimator>();
                _baseColor = Color.white;

                var star = art!.Get("Items/star");
                _swing = SpriteFactory.CreateRenderer("Swing", transform, star ?? SpriteFactory.Circle(), Color.white, star != null ? 0.7f : 1.4f, star != null ? 0.7f : 1.0f, 9);
                _swing.transform.localPosition = new Vector3(0.95f, 0.3f, 0f);
            }
            else
            {
                _body = SpriteFactory.CreateRenderer("Body", transform, SpriteFactory.RoundedRect(), FallbackBodyColor, FallbackWidth, FallbackHeight, 10);
                var eye = SpriteFactory.CreateRenderer("Eye", _body.transform, SpriteFactory.Square(), new Color(0.15f, 0.15f, 0.2f), 0.14f, 0.12f, 11);
                eye.transform.localPosition = new Vector3(0.22f, 0.72f, 0f);
                _swing = SpriteFactory.CreateRenderer("Swing", transform, SpriteFactory.Circle(), new Color(1f, 1f, 1f, 0.6f), 1.4f, 1.0f, 9);
                _swing.transform.localPosition = new Vector3(1.0f, 0.3f, 0f);
            }

            _swing.enabled = false;
            _lastY = simulation.Motor.Y;
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

            if (_art != null && _animator != null)
            {
                ChoosePose(motor, player);
            }
            else
            {
                _body.transform.localScale = new Vector3(FallbackWidth, motor.IsCrouching ? FallbackHeight * 0.65f : FallbackHeight, 1f);
            }

            var color = _baseColor;
            if (player.IsDead) color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
            else if (player.IsInvulnerable && Mathf.FloorToInt(Time.time * 12f) % 2 == 0) color = new Color(1f, 0.7f, 0.7f, 0.6f);
            else if (_art == null && motor.Mode == MotorMode.Ladder) color = new Color(0.85f, 0.85f, 0.65f);
            _body.color = color;

            _swing.enabled = !player.IsDead && motor.IsAttackLocked;
            if (_swing.enabled)
            {
                _swing.transform.localRotation = Quaternion.Euler(0f, 0f, (Time.time * 720f) % 360f);
            }
        }

        private void ChoosePose(CharacterMotor motor, PlayerState player)
        {
            var art = _art!;
            var animator = _animator!;

            if (player.IsDead)
            {
                animator.Show(art.Hurt);
            }
            else
            {
                switch (motor.Mode)
                {
                    case MotorMode.Ladder:
                        animator.Play(art.Climb);
                        animator.Paused = Mathf.Abs(motor.Y - _lastY) < 0.0001f;
                        break;
                    case MotorMode.Air:
                        animator.Show(art.Jump);
                        break;
                    default:
                        if (motor.IsCrouching) animator.Show(art.Duck);
                        else if (Mathf.Abs(motor.VelocityX) > 0.01f && !motor.IsAttackLocked)
                        {
                            animator.Paused = false;
                            animator.Play(art.Walk);
                        }
                        else animator.Show(art.Stand);
                        break;
                }
            }

            _lastY = motor.Y;
        }
    }

    /// <summary>敵 1 体の見た目。</summary>
    public sealed class EnemyView : MonoBehaviour
    {
        private const float DeathDisplaySeconds = 0.8f;

        private EnemyEntity? _enemy;
        private EnemyArt? _art;
        private SpriteRenderer? _body;
        private SpriteAnimator? _animator;
        private Color _baseColor = Color.white;
        private bool _wasDead;
        private float _deathTimer;

        public EnemyEntity? Enemy => _enemy;

        public void Bind(EnemyEntity enemy, ArtLibrary? art)
        {
            _enemy = enemy;
            _art = art?.GetEnemy(enemy.Definition.EnemyId);

            if (_art != null)
            {
                var bodyGo = new GameObject("Body");
                bodyGo.transform.SetParent(transform, false);
                _body = bodyGo.AddComponent<SpriteRenderer>();
                _body.sprite = _art.Idle;
                _body.sortingOrder = 8;
                _animator = bodyGo.AddComponent<SpriteAnimator>();
                _animator.Play(_art.Walk);
                _baseColor = Color.white;
            }
            else
            {
                var (sprite, color, width, height) = FallbackLook(enemy.Definition);
                _baseColor = color;
                _body = SpriteFactory.CreateRenderer("Body", transform, sprite, color, width, height, 8);
                var eye = SpriteFactory.CreateRenderer("Eye", _body.transform, SpriteFactory.Square(), new Color(0.1f, 0.1f, 0.1f), 0.12f, 0.12f, 9);
                eye.transform.localPosition = new Vector3(-0.2f, 0.55f, 0f);
            }

            _wasDead = enemy.IsDead;
            Sync();
        }

        public void Sync()
        {
            if (_enemy == null || _body == null) return;

            if (_enemy.IsDead && !_wasDead) _deathTimer = DeathDisplaySeconds;
            _wasDead = _enemy.IsDead;

            if (_enemy.IsDead)
            {
                if (_art != null && _animator != null && _deathTimer > 0f)
                {
                    _deathTimer -= Time.deltaTime;
                    gameObject.SetActive(true);
                    _animator.Show(_art.Dead);
                    _body.color = new Color(1f, 1f, 1f, Mathf.Clamp01(_deathTimer / DeathDisplaySeconds));
                    return;
                }
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            transform.position = new Vector3(_enemy.X, _enemy.Y, 0f);
            // 素の絵は左向き(コード生成の目も左側)なので、右を向くときに反転する
            transform.localScale = new Vector3(_enemy.Facing == Direction.Right ? -1f : 1f, 1f, 1f);

            if (_art != null && _animator != null)
            {
                if (_enemy.HitFlash > 0f) _animator.Show(_art.Hit);
                else _animator.Play(_art.Walk);
                _body.color = _enemy.HitFlash > 0f ? new Color(1f, 0.6f, 0.6f) : _baseColor;
            }
            else
            {
                _body.color = _enemy.HitFlash > 0f ? Color.white : _baseColor;
            }
        }

        private static (Sprite, Color, float, float) FallbackLook(EnemyDefinition definition)
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

        public void Bind(ItemDrop drop, ArtLibrary? art)
        {
            _drop = drop;
            _phase = drop.DropId * 0.7f;
            var sprite = art?.Item(drop.ItemId);
            if (sprite != null)
            {
                SpriteFactory.CreateRenderer("Item", transform, sprite, Color.white, 0.55f, 0.55f, 7);
            }
            else
            {
                SpriteFactory.CreateRenderer("Item", transform, SpriteFactory.Diamond(), new Color(1f, 0.85f, 0.25f), 0.45f, 0.45f, 7);
            }
            Sync();
        }

        public void Sync()
        {
            if (_drop == null) return;
            var bob = Mathf.Sin(Time.time * 4f + _phase) * 0.08f + 0.1f;
            transform.position = new Vector3(_drop.X, _drop.Y + bob, 0f);
        }
    }

    /// <summary>
    /// 敵とドロップの見た目をシミュレーションと同期させる。
    /// オンラインでは敵がスナップショットや湧きの通知で後から増えたり入れ替わったりするので、毎回突き合わせて作り・消す。
    /// </summary>
    public sealed class WorldViewSync
    {
        private readonly Transform _root;
        private readonly ArtLibrary? _art;
        private readonly Dictionary<EnemyEntity, EnemyView> _enemies = new Dictionary<EnemyEntity, EnemyView>();
        private readonly Dictionary<int, DropView> _drops = new Dictionary<int, DropView>();
        private readonly List<EnemyEntity> _removedEnemies = new List<EnemyEntity>();
        private readonly HashSet<EnemyEntity> _alive = new HashSet<EnemyEntity>();
        private readonly HashSet<int> _dropIds = new HashSet<int>();
        private readonly List<int> _removedDrops = new List<int>();
        private readonly List<EnemyView> _enemyList = new List<EnemyView>();

        public WorldViewSync(Transform root, WorldState world, ArtLibrary? art)
        {
            _root = root;
            _art = art;
            Sync(world);
        }

        public IReadOnlyList<EnemyView> Enemies => _enemyList;

        public void Sync(WorldState world)
        {
            // 敵: 増えた分を作り、いなくなった分を消す
            _alive.Clear();
            foreach (var enemy in world.Enemies)
            {
                _alive.Add(enemy);
                if (_enemies.ContainsKey(enemy)) continue;
                var go = new GameObject($"Enemy {enemy.Definition.Name}#{enemy.InstanceId}");
                go.transform.SetParent(_root, false);
                var view = go.AddComponent<EnemyView>();
                view.Bind(enemy, _art);
                _enemies[enemy] = view;
            }

            _removedEnemies.Clear();
            foreach (var pair in _enemies)
            {
                if (_alive.Contains(pair.Key)) pair.Value.Sync();
                else _removedEnemies.Add(pair.Key);
            }
            foreach (var enemy in _removedEnemies)
            {
                if (_enemies[enemy] != null) Object.Destroy(_enemies[enemy].gameObject);
                _enemies.Remove(enemy);
            }
            if (_removedEnemies.Count > 0 || _enemyList.Count != _enemies.Count)
            {
                _enemyList.Clear();
                _enemyList.AddRange(_enemies.Values);
            }

            // 落とし物
            _dropIds.Clear();
            foreach (var drop in world.Drops)
            {
                _dropIds.Add(drop.DropId);
                if (_drops.ContainsKey(drop.DropId)) continue;
                var go = new GameObject($"Drop item{drop.ItemId}#{drop.DropId}");
                go.transform.SetParent(_root, false);
                var view = go.AddComponent<DropView>();
                view.Bind(drop, _art);
                _drops[drop.DropId] = view;
            }

            _removedDrops.Clear();
            foreach (var pair in _drops)
            {
                if (_dropIds.Contains(pair.Key)) pair.Value.Sync();
                else _removedDrops.Add(pair.Key);
            }
            foreach (var id in _removedDrops)
            {
                if (_drops[id] != null) Object.Destroy(_drops[id].gameObject);
                _drops.Remove(id);
            }
        }
    }
}
