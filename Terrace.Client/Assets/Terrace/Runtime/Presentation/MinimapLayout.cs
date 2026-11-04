using System;
using System.Collections.Generic;
using Terrace.Map;

namespace Terrace.Client.Presentation
{
    /// <summary>ミニマップに描く線の種類。</summary>
    public enum MinimapLineKind
    {
        /// <summary>足場(壁を含む)。</summary>
        Foothold,
        /// <summary>はしご・ロープ。</summary>
        Ladder,
    }

    /// <summary>ミニマップに描く、動かない点の種類。</summary>
    public enum MinimapMarkerKind
    {
        /// <summary>入れるポータル(出現地点は除く)。</summary>
        Portal,
        Npc,
    }

    /// <summary>ミニマップ上の線。座標はミニマップの左下を原点にした画素。</summary>
    public readonly struct MinimapLine
    {
        public MinimapLine(MinimapLineKind kind, float x1, float y1, float x2, float y2)
        {
            Kind = kind;
            X1 = x1;
            Y1 = y1;
            X2 = x2;
            Y2 = y2;
        }

        public MinimapLineKind Kind { get; }
        public float X1 { get; }
        public float Y1 { get; }
        public float X2 { get; }
        public float Y2 { get; }
    }

    /// <summary>ミニマップ上の動かない点。座標はミニマップの左下を原点にした画素。</summary>
    public readonly struct MinimapMarker
    {
        public MinimapMarker(MinimapMarkerKind kind, float x, float y)
        {
            Kind = kind;
            X = x;
            Y = y;
        }

        public MinimapMarkerKind Kind { get; }
        public float X { get; }
        public float Y { get; }
    }

    /// <summary>
    /// ミニマップの見せ方の計算(UnityEngine を使わない。.NET のテストで確かめる)。
    ///
    /// 描く範囲は、マップの境界ではなく「足場・はしご・ポータル・NPC を囲む範囲 + 余白」。境界の上の広い空を描かないため。
    /// 大きさは「1 ユニットあたりの画素数」で決め、最大の幅と高さを超えるなら縦横比を保って縮める。
    /// 座標はミニマップの左下を原点にした画素で、範囲の外の点は縁に寄せる。
    /// </summary>
    public sealed class MinimapLayout
    {
        private readonly List<MinimapLine> _lines = new List<MinimapLine>();
        private readonly List<MinimapMarker> _markers = new List<MinimapMarker>();

        private MinimapLayout(float left, float bottom, float right, float top, float scale)
        {
            Left = left;
            Bottom = bottom;
            Right = right;
            Top = top;
            Scale = scale;
        }

        /// <summary>描く範囲(ワールド座標)。</summary>
        public float Left { get; }
        public float Bottom { get; }
        public float Right { get; }
        public float Top { get; }

        /// <summary>1 ユニットあたりの画素数(縮めた後)。</summary>
        public float Scale { get; }

        /// <summary>ミニマップの幅と高さ(画素)。</summary>
        public float Width => (Right - Left) * Scale;
        public float Height => (Top - Bottom) * Scale;

        /// <summary>足場とはしごの線。</summary>
        public IReadOnlyList<MinimapLine> Lines => _lines;

        /// <summary>ポータルと NPC の点。</summary>
        public IReadOnlyList<MinimapMarker> Markers => _markers;

        /// <param name="map">描くマップ。</param>
        /// <param name="margin">中身を囲む範囲の外に足す余白(ユニット)。</param>
        /// <param name="pixelsPerUnit">1 ユニットあたりの画素数(縮める前)。</param>
        /// <param name="maxWidth">ミニマップの最大の幅(画素)。</param>
        /// <param name="maxHeight">ミニマップの最大の高さ(画素)。</param>
        public static MinimapLayout Create(MapData map, float margin, float pixelsPerUnit, float maxWidth, float maxHeight)
        {
            var (left, bottom, right, top) = ContentRange(map);
            left -= margin;
            bottom -= margin;
            right += margin;
            top += margin;

            var width = right - left;
            var height = top - bottom;
            var scale = pixelsPerUnit;
            if (width * scale > maxWidth || height * scale > maxHeight)
            {
                scale = Math.Min(maxWidth / width, maxHeight / height);
            }

            var layout = new MinimapLayout(left, bottom, right, top, scale);
            layout.AddContent(map);
            return layout;
        }

        /// <summary>ワールド座標をミニマップ上の画素へ。範囲の外は縁に寄せる。</summary>
        public (float X, float Y) ToMinimap(float worldX, float worldY)
        {
            var x = (Clamp(worldX, Left, Right) - Left) * Scale;
            var y = (Clamp(worldY, Bottom, Top) - Bottom) * Scale;
            return (x, y);
        }

        /// <summary>足場・はしご・ポータル・NPC を囲む範囲。何も無ければマップの境界(それも無ければ 1 ユニット四方)。幅や高さが 0 なら 1 ユニットに広げる。</summary>
        private static (float Left, float Bottom, float Right, float Top) ContentRange(MapData map)
        {
            var left = float.PositiveInfinity;
            var bottom = float.PositiveInfinity;
            var right = float.NegativeInfinity;
            var top = float.NegativeInfinity;

            void Include(float x, float y)
            {
                left = Math.Min(left, x);
                right = Math.Max(right, x);
                bottom = Math.Min(bottom, y);
                top = Math.Max(top, y);
            }

            foreach (var foothold in map.Footholds)
            {
                Include(foothold.X1, foothold.Y1);
                Include(foothold.X2, foothold.Y2);
            }
            foreach (var ladder in map.Ladders)
            {
                Include(ladder.X, ladder.Y1);
                Include(ladder.X, ladder.Y2);
            }
            foreach (var portal in map.Portals) Include(portal.X, portal.Y);
            foreach (var npc in map.Npcs) Include(npc.X, npc.Y);

            if (left > right)
            {
                var bounds = map.Bounds;
                if (bounds != null && bounds.IsValid) return (bounds.Left, bounds.Bottom, bounds.Right, bounds.Top);
                return (0f, 0f, 1f, 1f);
            }

            if (right - left < 1f)
            {
                var center = (left + right) * 0.5f;
                left = center - 0.5f;
                right = center + 0.5f;
            }
            if (top - bottom < 1f)
            {
                var center = (bottom + top) * 0.5f;
                bottom = center - 0.5f;
                top = center + 0.5f;
            }
            return (left, bottom, right, top);
        }

        private void AddContent(MapData map)
        {
            foreach (var foothold in map.Footholds)
            {
                var (x1, y1) = ToMinimap(foothold.X1, foothold.Y1);
                var (x2, y2) = ToMinimap(foothold.X2, foothold.Y2);
                _lines.Add(new MinimapLine(MinimapLineKind.Foothold, x1, y1, x2, y2));
            }
            foreach (var ladder in map.Ladders)
            {
                var (x1, y1) = ToMinimap(ladder.X, ladder.Y1);
                var (x2, y2) = ToMinimap(ladder.X, ladder.Y2);
                _lines.Add(new MinimapLine(MinimapLineKind.Ladder, x1, y1, x2, y2));
            }
            foreach (var portal in map.Portals)
            {
                if (portal.IsSpawn) continue;
                var (x, y) = ToMinimap(portal.X, portal.Y);
                _markers.Add(new MinimapMarker(MinimapMarkerKind.Portal, x, y));
            }
            foreach (var npc in map.Npcs)
            {
                var (x, y) = ToMinimap(npc.X, npc.Y);
                _markers.Add(new MinimapMarker(MinimapMarkerKind.Npc, x, y));
            }
        }

        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }
}
