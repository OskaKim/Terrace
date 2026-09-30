using MessagePack;

namespace Terrace.Shared
{
    public enum Facing
    {
        Left = -1,
        Right = 1,
    }

    public enum MotionState
    {
        Stand = 0,
        Walk = 1,
        Jump = 2,
        Ladder = 3,
        Crouch = 4,
        Attack = 5,
        Dead = 6,
    }

    /// <summary>
    /// クライアントが計算した移動状態(座標・速度・向き・状態)。
    /// 移動はクライアント権威なので、サーバーはこれをそのまま他のクライアントへ中継する。
    /// </summary>
    [MessagePackObject]
    public sealed class MoveState
    {
        [Key(0)]
        public float X { get; set; }

        [Key(1)]
        public float Y { get; set; }

        [Key(2)]
        public float VelocityX { get; set; }

        [Key(3)]
        public float VelocityY { get; set; }

        [Key(4)]
        public Facing Facing { get; set; } = Facing.Right;

        [Key(5)]
        public MotionState Motion { get; set; }

        public MoveState Clone() => (MoveState)MemberwiseClone();

        public override string ToString() => $"x={X:F1} y={Y:F1} vx={VelocityX:F1} vy={VelocityY:F1} facing={Facing} state={Motion}";
    }
}
