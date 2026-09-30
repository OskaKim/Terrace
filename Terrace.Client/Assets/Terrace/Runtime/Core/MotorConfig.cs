namespace Terrace.Client.Core
{
    /// <summary>キャラクターの移動パラメータ。単位はマップと同じ(1.0 = 1 ユニット、秒)。</summary>
    public sealed class MotorConfig
    {
        public float WalkSpeed { get; set; } = 5f;
        public float JumpVelocity { get; set; } = 11f;
        public float Gravity { get; set; } = 30f;
        public float TerminalVelocity { get; set; } = 30f;
        public float ClimbSpeed { get; set; } = 3.5f;

        /// <summary>はしごをつかめる X の距離。</summary>
        public float LadderGrabRange { get; set; } = 0.6f;

        /// <summary>ポータルに入れる距離。</summary>
        public float PortalRange { get; set; } = 1.0f;

        /// <summary>攻撃モーション中に移動できない時間。</summary>
        public float AttackLockSeconds { get; set; } = 0.35f;

        public float LadderJumpVelocityX { get; set; } = 3.5f;
        public float LadderJumpVelocityY { get; set; } = 6f;

        /// <summary>ポータルを使った直後に再入場できない時間。</summary>
        public float PortalCooldownSeconds { get; set; } = 1f;

        /// <summary>ワールド下端からこれだけ落ちたら落死とみなす。</summary>
        public float FallDeathMargin { get; set; } = 5f;

        public static MotorConfig Default => new MotorConfig();
    }
}
