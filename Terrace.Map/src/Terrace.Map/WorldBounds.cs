namespace Terrace.Map
{
    /// <summary>ワールド境界。Y が上なので Top &gt; Bottom。</summary>
    public sealed class WorldBounds
    {
        public float Left { get; set; }
        public float Right { get; set; }
        public float Top { get; set; }
        public float Bottom { get; set; }

        public float Width => Right - Left;
        public float Height => Top - Bottom;
        public bool IsValid => Left < Right && Bottom < Top;

        public bool Contains(float x, float y) => x >= Left && x <= Right && y >= Bottom && y <= Top;
        public bool Contains(Position position) => Contains(position.X, position.Y);

        public Position Clamp(Position position)
        {
            var x = position.X < Left ? Left : position.X > Right ? Right : position.X;
            var y = position.Y < Bottom ? Bottom : position.Y > Top ? Top : position.Y;
            return new Position(x, y);
        }

        public override string ToString() => $"Bounds L={Left} R={Right} T={Top} B={Bottom}";
    }
}
