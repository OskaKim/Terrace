using System;
using Terrace.Map;

namespace Terrace.Client.Core
{
    public enum MotorMode
    {
        Ground,
        Air,
        Ladder,
    }

    /// <summary>1 ステップで起きた出来事。</summary>
    public struct MotorEvents
    {
        public bool Jumped;
        public bool Landed;
        public bool GrabbedLadder;
        public bool AttackStarted;
        public bool FellOutOfWorld;
        public Portal? EnteredPortal;
    }

    /// <summary>
    /// フットホールド(線分)の上を歩くキャラクターの移動。物理エンジンは使わず Terrace.Map の問い合わせだけで動く。
    /// メイプルストーリー準拠の割り切り:
    /// - 空中では横方向を変えられない(ジャンプ時の速度を保つ)
    /// - 崖の端から歩き出すと落ちる。壁(垂直な線分)には止まる
    /// - ↓+ジャンプで下の足場へ飛び降りる
    /// - ↑ではしごをつかむ / ポータルに入る。はしごの上で ←→+ジャンプで飛び降りる
    /// - 攻撃中は歩けない
    /// </summary>
    public sealed class CharacterMotor
    {
        private const float Epsilon = 0.05f;

        private readonly MapData _map;
        private readonly MotorConfig _config;
        private Foothold? _dropThrough;
        private float _attackLock;
        private float _portalCooldown;

        public CharacterMotor(MapData map, MotorConfig config, float x, float y)
        {
            _map = map;
            _config = config;
            Facing = Direction.Right;
            Teleport(x, y);
            _portalCooldown = 0f;
        }

        public float X { get; private set; }
        public float Y { get; private set; }
        public float VelocityX { get; private set; }
        public float VelocityY { get; private set; }
        public Direction Facing { get; private set; }
        public MotorMode Mode { get; private set; }
        public Foothold? Ground { get; private set; }
        public Ladder? Ladder { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsAttackLocked => _attackLock > 0f;
        public MotorConfig Config => _config;
        public Position Position => new Position(X, Y);

        /// <summary>指定位置へ移す。足場の上ならそこに立ち、無ければ落下を始める。</summary>
        public void Teleport(float x, float y)
        {
            X = x;
            Y = y;
            VelocityX = 0f;
            VelocityY = 0f;
            _dropThrough = null;
            Ladder = null;

            var ground = _map.FindFootholdBelow(x, y);
            if (ground != null && Math.Abs(ground.GetYAt(x) - y) < 0.5f)
            {
                Land(ground, x);
            }
            else
            {
                Mode = MotorMode.Air;
                Ground = null;
            }
            _portalCooldown = _config.PortalCooldownSeconds;
        }

        /// <summary>吹き飛ばす(ノックバック)。</summary>
        public void Launch(float velocityX, float velocityY)
        {
            Mode = MotorMode.Air;
            Ground = null;
            Ladder = null;
            IsCrouching = false;
            VelocityX = velocityX;
            VelocityY = velocityY;
        }

        public MotorEvents Step(in InputFrame input, float dt)
        {
            var events = new MotorEvents();
            if (_attackLock > 0f) _attackLock -= dt;
            if (_portalCooldown > 0f) _portalCooldown -= dt;

            if (input.AttackPressed && Mode != MotorMode.Ladder && _attackLock <= 0f)
            {
                _attackLock = _config.AttackLockSeconds;
                events.AttackStarted = true;
                if (Mode == MotorMode.Ground) VelocityX = 0f;
            }

            switch (Mode)
            {
                case MotorMode.Ground:
                    StepGround(input, dt, ref events);
                    break;
                case MotorMode.Air:
                    StepAir(input, dt, ref events);
                    break;
                case MotorMode.Ladder:
                    StepLadder(input, dt, ref events);
                    break;
            }

            ClampToWorld(ref events);
            return events;
        }

        private void StepGround(in InputFrame input, float dt, ref MotorEvents events)
        {
            IsCrouching = false;
            var ground = Ground!;

            // ↑ でポータルに入る
            if (input.Up && _portalCooldown <= 0f)
            {
                var portal = _map.FindPortalNear(X, Y, _config.PortalRange);
                if (portal != null)
                {
                    events.EnteredPortal = portal;
                    _portalCooldown = _config.PortalCooldownSeconds;
                    return;
                }
            }

            // ↑ ではしごを登り始める / ↓ ではしごを降り始める
            if (input.Up || input.Down)
            {
                var ladder = _map.FindLadderNear(X, Y, _config.LadderGrabRange);
                if (ladder != null)
                {
                    var canGrab = input.Up ? Y < ladder.Top - Epsilon : Y > ladder.Bottom + Epsilon;
                    if (canGrab)
                    {
                        GrabLadder(ladder);
                        events.GrabbedLadder = true;
                        return;
                    }
                }
            }

            if (input.JumpPressed && !IsAttackLocked)
            {
                if (input.Down)
                {
                    // ↓+ジャンプ: 下に足場があれば飛び降りる
                    var below = FindFootholdBelowExcluding(X, Y - Epsilon, ground);
                    if (below != null)
                    {
                        _dropThrough = ground;
                        Mode = MotorMode.Air;
                        Ground = null;
                        VelocityX = 0f;
                        VelocityY = 0f;
                        Y -= Epsilon;
                        events.Jumped = true;
                        return;
                    }
                }
                else
                {
                    var axis = input.HorizontalAxis;
                    if (axis != 0) Facing = axis > 0 ? Direction.Right : Direction.Left;
                    Mode = MotorMode.Air;
                    Ground = null;
                    VelocityX = axis * _config.WalkSpeed;
                    VelocityY = _config.JumpVelocity;
                    events.Jumped = true;
                    return;
                }
            }

            if (input.Down)
            {
                IsCrouching = true;
                VelocityX = 0f;
                return;
            }

            var horizontal = input.HorizontalAxis;
            if (horizontal == 0 || IsAttackLocked)
            {
                VelocityX = 0f;
                Y = ground.GetYAt(X);
                return;
            }

            Facing = horizontal > 0 ? Direction.Right : Direction.Left;
            MoveAlongGround(horizontal * _config.WalkSpeed * dt, dt);
        }

        private void MoveAlongGround(float dx, float dt)
        {
            var ground = Ground!;
            var direction = dx > 0f ? Direction.Right : Direction.Left;
            var targetX = X + dx;
            VelocityX = dt > 0f ? dx / dt : 0f;

            if (ground.IsWithinX(targetX))
            {
                X = targetX;
                Y = ground.GetYAt(X);
                return;
            }

            var next = _map.GetNext(ground, direction);
            if (next == null)
            {
                // 崖: 歩き出してそのまま落ちる
                X = targetX;
                Mode = MotorMode.Air;
                Ground = null;
                VelocityY = 0f;
                return;
            }

            if (next.IsVertical)
            {
                // 壁: 端で止まる
                X = direction == Direction.Right ? ground.Right : ground.Left;
                Y = ground.GetYAt(X);
                VelocityX = 0f;
                return;
            }

            // 次の足場へ乗り換える
            Ground = next;
            X = next.IsWithinX(targetX) ? targetX : (direction == Direction.Right ? next.Right : next.Left);
            Y = next.GetYAt(X);
        }

        private void StepAir(in InputFrame input, float dt, ref MotorEvents events)
        {
            // 空中でも ↑ ではしごをつかめる
            if (input.Up)
            {
                var ladder = _map.FindLadderNear(X, Y, _config.LadderGrabRange);
                if (ladder != null && Y >= ladder.Bottom && Y <= ladder.Top)
                {
                    GrabLadder(ladder);
                    events.GrabbedLadder = true;
                    return;
                }
            }

            VelocityY = Math.Max(VelocityY - _config.Gravity * dt, -_config.TerminalVelocity);
            var newX = X + VelocityX * dt;
            var newY = Y + VelocityY * dt;

            if (VelocityY <= 0f)
            {
                var landing = FindLanding(newX, Y, newY);
                if (landing != null)
                {
                    Land(landing, newX);
                    events.Landed = true;
                    return;
                }
            }

            X = newX;
            Y = newY;

            if (_dropThrough != null && (!_dropThrough.IsWithinX(X) || Y < _dropThrough.GetYAt(X) - 0.3f))
            {
                _dropThrough = null;
            }
        }

        private void StepLadder(in InputFrame input, float dt, ref MotorEvents events)
        {
            var ladder = Ladder!;
            X = ladder.X;
            VelocityX = 0f;
            VelocityY = 0f;
            IsCrouching = false;

            // ←→+ジャンプで飛び降りる
            if (input.JumpPressed && input.HorizontalAxis != 0)
            {
                var axis = input.HorizontalAxis;
                Facing = axis > 0 ? Direction.Right : Direction.Left;
                Mode = MotorMode.Air;
                Ladder = null;
                VelocityX = axis * _config.LadderJumpVelocityX;
                VelocityY = _config.LadderJumpVelocityY;
                events.Jumped = true;
                return;
            }

            var vertical = input.VerticalAxis;
            if (vertical == 0) return;

            Y += vertical * _config.ClimbSpeed * dt;

            if (Y >= ladder.Top)
            {
                Y = ladder.Top;
                var top = FindFootholdAt(X, ladder.Top);
                if (top != null)
                {
                    Land(top, X);
                    events.Landed = true;
                }
            }
            else if (Y <= ladder.Bottom)
            {
                Y = ladder.Bottom;
                var bottom = FindFootholdAt(X, ladder.Bottom);
                if (bottom != null)
                {
                    Land(bottom, X);
                    events.Landed = true;
                }
                else
                {
                    Mode = MotorMode.Air;
                    Ladder = null;
                }
            }
        }

        private void GrabLadder(Ladder ladder)
        {
            Mode = MotorMode.Ladder;
            Ladder = ladder;
            Ground = null;
            _dropThrough = null;
            IsCrouching = false;
            X = ladder.X;
            Y = Math.Min(Math.Max(Y, ladder.Bottom), ladder.Top);
            VelocityX = 0f;
            VelocityY = 0f;
        }

        private void Land(Foothold foothold, float x)
        {
            Mode = MotorMode.Ground;
            Ground = foothold;
            Ladder = null;
            _dropThrough = null;
            X = x;
            Y = foothold.GetYAt(x);
            VelocityX = 0f;
            VelocityY = 0f;
        }

        /// <summary>fromY から toY へ落ちる間に横切る足場のうち一番上のもの。</summary>
        private Foothold? FindLanding(float x, float fromY, float toY)
        {
            Foothold? best = null;
            var bestY = float.NegativeInfinity;
            foreach (var foothold in _map.Footholds)
            {
                if (foothold.IsVertical || !foothold.IsWithinX(x)) continue;
                if (ReferenceEquals(foothold, _dropThrough)) continue;
                var yAt = foothold.GetYAt(x);
                if (yAt > fromY + Epsilon || yAt < toY) continue;
                if (yAt > bestY)
                {
                    best = foothold;
                    bestY = yAt;
                }
            }
            return best;
        }

        private Foothold? FindFootholdBelowExcluding(float x, float y, Foothold exclude)
        {
            Foothold? best = null;
            var bestY = float.NegativeInfinity;
            foreach (var foothold in _map.Footholds)
            {
                if (foothold.IsVertical || ReferenceEquals(foothold, exclude) || !foothold.IsWithinX(x)) continue;
                var yAt = foothold.GetYAt(x);
                if (yAt > y) continue;
                if (yAt > bestY)
                {
                    best = foothold;
                    bestY = yAt;
                }
            }
            return best;
        }

        private Foothold? FindFootholdAt(float x, float y)
        {
            foreach (var foothold in _map.Footholds)
            {
                if (foothold.IsVertical || !foothold.IsWithinX(x)) continue;
                if (Math.Abs(foothold.GetYAt(x) - y) <= 0.1f) return foothold;
            }
            return null;
        }

        private void ClampToWorld(ref MotorEvents events)
        {
            var bounds = _map.Bounds;
            if (bounds == null || !bounds.IsValid) return;

            if (X < bounds.Left)
            {
                X = bounds.Left;
                if (VelocityX < 0f) VelocityX = 0f;
            }
            else if (X > bounds.Right)
            {
                X = bounds.Right;
                if (VelocityX > 0f) VelocityX = 0f;
            }

            if (Y > bounds.Top)
            {
                Y = bounds.Top;
                if (VelocityY > 0f) VelocityY = 0f;
            }

            if (Y < bounds.Bottom - _config.FallDeathMargin)
            {
                events.FellOutOfWorld = true;
            }
        }
    }
}
