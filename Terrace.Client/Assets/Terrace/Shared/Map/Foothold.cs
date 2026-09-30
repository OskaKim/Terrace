using System;

namespace Terrace.Map
{
    /// <summary>
    /// 足場(線分)。地面や床は線分の連なりで表し、線分の端が前後のフットホールドに繋がる。
    ///
    /// - PrevId は (X1, Y1) 側、NextId は (X2, Y2) 側に繋がる相手の Id。0 ならそこがチェーンの端(= 崖)
    /// - X1 != X2 なら歩ける線分。Y1 != Y2 なら坂道
    /// - X1 == X2 は壁として扱い、真下検索(<see cref="MapData.FindFootholdBelow"/>)の対象にしない
    /// - 向き(Left / Right)はジオメトリで解決する。X1 &lt;= X2 なら Right 側の端は (X2, Y2) = NextId、
    ///   X1 &gt; X2 なら Right 側の端は (X1, Y1) = PrevId
    /// </summary>
    public sealed class Foothold
    {
        public int Id { get; set; }
        public float X1 { get; set; }
        public float Y1 { get; set; }
        public float X2 { get; set; }
        public float Y2 { get; set; }

        /// <summary>(X1, Y1) 側に繋がる Foothold の Id。0 なら崖。</summary>
        public int PrevId { get; set; }

        /// <summary>(X2, Y2) 側に繋がる Foothold の Id。0 なら崖。</summary>
        public int NextId { get; set; }

        public int Layer { get; set; }

        public Position Start => new Position(X1, Y1);
        public Position End => new Position(X2, Y2);
        public float Left => Math.Min(X1, X2);
        public float Right => Math.Max(X1, X2);
        public float Bottom => Math.Min(Y1, Y2);
        public float Top => Math.Max(Y1, Y2);
        public float Width => Right - Left;
        public bool IsVertical => X1 == X2;
        public bool IsFlat => Y1 == Y2;

        /// <summary>傾き dy/dx。垂直なら正の無限大。</summary>
        public float Slope => IsVertical ? float.PositiveInfinity : (Y2 - Y1) / (X2 - X1);

        /// <summary>X が線分の範囲内(両端を含む)か。</summary>
        public bool IsWithinX(float x) => x >= Left && x <= Right;

        /// <summary>線分上の指定 X における Y(線形補間)。X が範囲外なら近い方の端の Y。垂直なら Top。</summary>
        public float GetYAt(float x)
        {
            if (IsVertical) return Top;
            var t = (x - X1) / (X2 - X1);
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
            return Y1 + (Y2 - Y1) * t;
        }

        /// <summary>direction 側の端点。Right は X が大きい方の端(垂直なら End)。</summary>
        public Position GetEnd(Direction direction) => IsNextSide(direction) ? End : Start;

        /// <summary>direction 側の端に繋がる Foothold の Id。0 なら崖。</summary>
        public int GetLinkedId(Direction direction) => IsNextSide(direction) ? NextId : PrevId;

        private bool IsNextSide(Direction direction)
        {
            var nextIsOnRight = X1 <= X2;
            return (direction == Direction.Right) == nextIsOnRight;
        }

        public override string ToString()
            => $"Foothold#{Id} ({X1}, {Y1})-({X2}, {Y2}) prev={PrevId} next={NextId} layer={Layer}";
    }
}
