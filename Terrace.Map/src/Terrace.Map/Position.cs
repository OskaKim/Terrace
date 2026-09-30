using System;

namespace Terrace.Map
{
    /// <summary>ワールド座標。X が右、Y が上。単位は無次元(1.0 = 1 ユニット)。</summary>
    public readonly struct Position : IEquatable<Position>
    {
        public static readonly Position Zero = new Position(0f, 0f);

        public Position(float x, float y)
        {
            X = x;
            Y = y;
        }

        public float X { get; }
        public float Y { get; }

        public float DistanceTo(Position other)
        {
            var dx = other.X - X;
            var dy = other.Y - Y;
            return MathF.Sqrt(dx * dx + dy * dy);
        }

        public Position WithX(float x) => new Position(x, Y);
        public Position WithY(float y) => new Position(X, y);

        public bool Equals(Position other) => X.Equals(other.X) && Y.Equals(other.Y);
        public override bool Equals(object? obj) => obj is Position other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(X, Y);
        public static bool operator ==(Position left, Position right) => left.Equals(right);
        public static bool operator !=(Position left, Position right) => !left.Equals(right);
        public override string ToString() => $"({X}, {Y})";
    }
}
