using System.Collections.Generic;
using NUnit.Framework;
using Terrace.Client.Core;
using Terrace.Map;

namespace Terrace.Client.Tests.EditMode
{
    [TestFixture]
    public class CharacterMotorTests
    {
        private const float Dt = 1f / 60f;

        private static CharacterMotor NewMotor(MapData map, float x, float y) => new CharacterMotor(map, MotorConfig.Default, x, y);

        /// <summary>入力を seconds 秒間与え続ける。途中で起きた出来事を集めて返す。</summary>
        private static List<MotorEvents> Run(CharacterMotor motor, InputFrame input, float seconds)
        {
            var events = new List<MotorEvents>();
            var steps = (int)(seconds / Dt + 0.5f);
            for (var i = 0; i < steps; i++)
            {
                events.Add(motor.Step(input, Dt));
                // 「押した瞬間」は最初のステップだけ
                input.JumpPressed = false;
                input.AttackPressed = false;
                input.PickupPressed = false;
            }
            return events;
        }

        [Test]
        public void 足場の上に置くと立つ()
        {
            var motor = NewMotor(TestMaps.Sample(), 5f, 0f);

            Assert.AreEqual(MotorMode.Ground, motor.Mode);
            Assert.AreEqual(1, motor.Ground!.Id);
            Assert.AreEqual(0f, motor.Y);
        }

        [Test]
        public void 右を押し続けると右へ歩く()
        {
            var motor = NewMotor(TestMaps.Sample(), 5f, 0f);

            Run(motor, InputFrame.Hold(right: true), 1f);

            Assert.AreEqual(10f, motor.X, 0.15f);
            Assert.AreEqual(0f, motor.Y, 0.001f);
            Assert.AreEqual(Direction.Right, motor.Facing);
            Assert.AreEqual(MotorMode.Ground, motor.Mode);
        }

        [Test]
        public void 坂道を歩くとYが線分に沿って上がり次の足場へ乗り換える()
        {
            var map = TestMaps.Sample();
            var motor = NewMotor(map, 18f, 4f); // 坂道 #2 (10,0)-(20,5) の x=18 は y=4
            Assert.AreEqual(2, motor.Ground!.Id);

            Run(motor, InputFrame.Hold(right: true), 0.3f);
            Assert.AreEqual(2, motor.Ground!.Id, "坂道(#2)の上にいる");
            Assert.AreEqual(map.FindFoothold(2)!.GetYAt(motor.X), motor.Y, 0.001f);
            Assert.Greater(motor.Y, 4f);

            Run(motor, InputFrame.Hold(right: true), 0.4f);
            Assert.AreEqual(3, motor.Ground!.Id, "坂を登り切って #3 に乗り換える");
            Assert.AreEqual(5f, motor.Y, 0.001f);
        }

        [Test]
        public void 左へ歩くと向きが変わる()
        {
            var motor = NewMotor(TestMaps.Sample(), 5f, 0f);

            Run(motor, InputFrame.Hold(left: true), 0.5f);

            Assert.AreEqual(Direction.Left, motor.Facing);
            Assert.AreEqual(2.5f, motor.X, 0.15f);
        }

        [Test]
        public void 崖から歩き出すと落下しワールドの外に出ると落死イベントが出る()
        {
            var motor = NewMotor(TestMaps.Sample(), 28f, 5f);

            var events = Run(motor, InputFrame.Hold(right: true), 3f);

            Assert.IsTrue(events.Exists(e => e.FellOutOfWorld));
            Assert.AreEqual(MotorMode.Air, motor.Mode);
        }

        [Test]
        public void ジャンプすると上昇してから同じ足場に着地する()
        {
            var motor = NewMotor(TestMaps.Sample(), 5f, 0f);

            var first = motor.Step(InputFrame.None.WithJump(), Dt);
            Assert.IsTrue(first.Jumped);
            Assert.AreEqual(MotorMode.Air, motor.Mode);

            Run(motor, InputFrame.None, 0.2f);
            Assert.Greater(motor.Y, 0.5f, "上昇している");

            var events = Run(motor, InputFrame.None, 2f);
            Assert.IsTrue(events.Exists(e => e.Landed));
            Assert.AreEqual(MotorMode.Ground, motor.Mode);
            Assert.AreEqual(0f, motor.Y, 0.001f);
            Assert.AreEqual(5f, motor.X, 0.001f, "真上に跳んだので X は変わらない");
        }

        [Test]
        public void 空中では横方向を変えられない()
        {
            var motor = NewMotor(TestMaps.Sample(), 3f, 0f);

            motor.Step(InputFrame.Hold(right: true).WithJump(), Dt);
            Assert.Greater(motor.VelocityX, 0f);

            Run(motor, InputFrame.Hold(left: true), 0.3f);

            Assert.AreEqual(MotorMode.Air, motor.Mode);
            Assert.Greater(motor.VelocityX, 0f, "左を押しても右向きの速度のまま");
            Assert.Greater(motor.X, 3f);
        }

        [Test]
        public void 攻撃中は歩けない()
        {
            var motor = NewMotor(TestMaps.Sample(), 5f, 0f);

            var events = motor.Step(InputFrame.Hold(right: true).WithAttack(), Dt);
            Assert.IsTrue(events.AttackStarted);
            Assert.IsTrue(motor.IsAttackLocked);

            Run(motor, InputFrame.Hold(right: true), 0.2f);
            Assert.AreEqual(5f, motor.X, 0.001f, "攻撃モーション中は動かない");

            Run(motor, InputFrame.Hold(right: true), 0.5f);
            Assert.Greater(motor.X, 5.5f, "ロックが解けたら歩ける");
        }

        [Test]
        public void 上ではしごをつかみ登り切ると上の足場に乗る()
        {
            var motor = NewMotor(TestMaps.Sample(), 30f, 5f);
            Assert.AreEqual(3, motor.Ground!.Id);

            var grab = motor.Step(InputFrame.Hold(up: true), Dt);
            Assert.IsTrue(grab.GrabbedLadder);
            Assert.AreEqual(MotorMode.Ladder, motor.Mode);
            Assert.AreEqual(30f, motor.X);

            Run(motor, InputFrame.Hold(up: true), 0.5f);
            Assert.AreEqual(MotorMode.Ladder, motor.Mode);
            Assert.Greater(motor.Y, 5.5f);

            var events = Run(motor, InputFrame.Hold(up: true), 2f);
            Assert.IsTrue(events.Exists(e => e.Landed));
            Assert.AreEqual(MotorMode.Ground, motor.Mode);
            Assert.AreEqual(4, motor.Ground!.Id, "はしごの上端の足場 #4 に乗る");
            Assert.AreEqual(10f, motor.Y, 0.001f);
        }

        [Test]
        public void 上の足場から下ではしごを降りて下の足場に立つ()
        {
            var motor = NewMotor(TestMaps.Sample(), 30f, 10f);
            Assert.AreEqual(4, motor.Ground!.Id);

            var grab = motor.Step(InputFrame.Hold(down: true), Dt);
            Assert.IsTrue(grab.GrabbedLadder);

            Run(motor, InputFrame.Hold(down: true), 2f);

            Assert.AreEqual(MotorMode.Ground, motor.Mode);
            Assert.AreEqual(3, motor.Ground!.Id);
            Assert.AreEqual(5f, motor.Y, 0.001f);
        }

        [Test]
        public void はしごの途中で横とジャンプで飛び降りる()
        {
            var motor = NewMotor(TestMaps.Sample(), 30f, 5f);
            motor.Step(InputFrame.Hold(up: true), Dt);
            Run(motor, InputFrame.Hold(up: true), 1.2f);
            Assert.AreEqual(MotorMode.Ladder, motor.Mode);

            var jump = motor.Step(InputFrame.Hold(left: true).WithJump(), Dt);
            Assert.IsTrue(jump.Jumped);
            Assert.AreEqual(MotorMode.Air, motor.Mode);
            Assert.Less(motor.VelocityX, 0f);

            Run(motor, InputFrame.None, 3f);
            Assert.AreEqual(MotorMode.Ground, motor.Mode);
            Assert.AreEqual(3, motor.Ground!.Id, "左へ飛び降りて #3 に着地する");
            Assert.Less(motor.X, 30f);
        }

        [Test]
        public void 下とジャンプで下の足場へ飛び降りる()
        {
            var motor = NewMotor(TestMaps.Sample(), 18f, 12f);
            Assert.AreEqual(6, motor.Ground!.Id);

            var drop = motor.Step(InputFrame.Hold(down: true).WithJump(), Dt);
            Assert.IsTrue(drop.Jumped);
            Assert.AreEqual(MotorMode.Air, motor.Mode);

            var events = Run(motor, InputFrame.None, 3f);
            Assert.IsTrue(events.Exists(e => e.Landed));
            Assert.AreEqual(2, motor.Ground!.Id, "斜面 #2 に着地する");
            Assert.AreEqual(18f, motor.X, 0.001f);
        }

        [Test]
        public void 下に足場が無ければ下とジャンプでも落ちない()
        {
            var motor = NewMotor(TestMaps.Sample(), 5f, 0f);

            motor.Step(InputFrame.Hold(down: true).WithJump(), Dt);

            Assert.AreEqual(MotorMode.Ground, motor.Mode);
            Assert.IsTrue(motor.IsCrouching);
        }

        [Test]
        public void 上でポータルに入るとイベントが出て連続では入れない()
        {
            var motor = NewMotor(TestMaps.Sample(), 2.5f, 0f);
            Run(motor, InputFrame.None, 1.1f); // 初期化直後のクールダウンを消化

            var enter = motor.Step(InputFrame.Hold(up: true), Dt);
            Assert.IsNotNull(enter.EnteredPortal);
            Assert.AreEqual("spawn", enter.EnteredPortal!.Name);

            var again = motor.Step(InputFrame.Hold(up: true), Dt);
            Assert.IsNull(again.EnteredPortal, "クールダウン中は入れない");
        }

        [Test]
        public void ワールドの横端で止まる()
        {
            var map = new MapData
            {
                Id = 1,
                Bounds = new WorldBounds { Left = 0, Right = 15, Top = 10, Bottom = -5 },
                Footholds = new List<Foothold> { new Foothold { Id = 1, X1 = 0, Y1 = 0, X2 = 20, Y2 = 0 } },
            };
            var motor = NewMotor(map, 12f, 0f);

            Run(motor, InputFrame.Hold(right: true), 2f);

            Assert.AreEqual(15f, motor.X, 0.001f);
            Assert.AreEqual(MotorMode.Ground, motor.Mode);
        }

        [Test]
        public void 壁に当たると端で止まる()
        {
            var map = new MapData
            {
                Id = 1,
                Bounds = new WorldBounds { Left = -5, Right = 30, Top = 20, Bottom = -5 },
                Footholds = new List<Foothold>
                {
                    new Foothold { Id = 1, X1 = 0, Y1 = 0, X2 = 10, Y2 = 0, NextId = 2 },
                    new Foothold { Id = 2, X1 = 10, Y1 = 0, X2 = 10, Y2 = 5, PrevId = 1 },
                },
            };
            var motor = NewMotor(map, 8f, 0f);

            Run(motor, InputFrame.Hold(right: true), 1f);

            Assert.AreEqual(10f, motor.X, 0.001f);
            Assert.AreEqual(MotorMode.Ground, motor.Mode);
            Assert.AreEqual(1, motor.Ground!.Id);
        }

        [Test]
        public void Teleportで空中に置くと落下して着地する()
        {
            var motor = NewMotor(TestMaps.Sample(), 5f, 0f);

            motor.Teleport(25f, 8f);
            Assert.AreEqual(MotorMode.Air, motor.Mode);

            Run(motor, InputFrame.None, 2f);
            Assert.AreEqual(MotorMode.Ground, motor.Mode);
            Assert.AreEqual(3, motor.Ground!.Id);
        }
    }
}
