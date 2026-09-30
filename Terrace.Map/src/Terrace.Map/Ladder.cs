using System;

namespace Terrace.Map
{
    /// <summary>はしご、またはロープ(IsRope)。X 固定で Y1〜Y2 の区間をつかまって上下できる。</summary>
    public sealed class Ladder
    {
        public int Id { get; set; }
        public float X { get; set; }
        public float Y1 { get; set; }
        public float Y2 { get; set; }
        public bool IsRope { get; set; }

        public float Bottom => Math.Min(Y1, Y2);
        public float Top => Math.Max(Y1, Y2);
        public float Height => Top - Bottom;

        /// <summary>Y が区間内(margin だけ広げた範囲)か。</summary>
        public bool ContainsY(float y, float margin = 0f) => y >= Bottom - margin && y <= Top + margin;

        public override string ToString() => $"{(IsRope ? "Rope" : "Ladder")}#{Id} x={X} y={Y1}..{Y2}";
    }
}
