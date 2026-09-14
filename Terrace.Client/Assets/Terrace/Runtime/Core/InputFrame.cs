namespace Terrace.Client.Core
{
    /// <summary>
    /// 1 フレーム分の入力。Unity の入力系に依存しないよう、押下状態だけを持つ。
    /// メイプルストーリー準拠: ←→ 移動、↑ はしご/ポータル、↓ しゃがみ/はしご降り、ジャンプ、攻撃、拾う。
    /// </summary>
    public struct InputFrame
    {
        public bool Left;
        public bool Right;
        public bool Up;
        public bool Down;

        /// <summary>ジャンプキーを押した瞬間。</summary>
        public bool JumpPressed;

        /// <summary>攻撃キーを押した瞬間。</summary>
        public bool AttackPressed;

        /// <summary>拾うキーを押した瞬間。</summary>
        public bool PickupPressed;

        /// <summary>-1 (左) / 0 / +1 (右)</summary>
        public int HorizontalAxis => (Right ? 1 : 0) - (Left ? 1 : 0);

        /// <summary>-1 (下) / 0 / +1 (上)</summary>
        public int VerticalAxis => (Up ? 1 : 0) - (Down ? 1 : 0);

        public static InputFrame None => default;

        public static InputFrame Hold(bool left = false, bool right = false, bool up = false, bool down = false)
            => new InputFrame { Left = left, Right = right, Up = up, Down = down };

        public InputFrame WithJump()
        {
            var frame = this;
            frame.JumpPressed = true;
            return frame;
        }

        public InputFrame WithAttack()
        {
            var frame = this;
            frame.AttackPressed = true;
            return frame;
        }

        public InputFrame WithPickup()
        {
            var frame = this;
            frame.PickupPressed = true;
            return frame;
        }

        public override string ToString()
            => $"h={HorizontalAxis} v={VerticalAxis}{(JumpPressed ? " jump" : "")}{(AttackPressed ? " attack" : "")}{(PickupPressed ? " pickup" : "")}";
    }
}
