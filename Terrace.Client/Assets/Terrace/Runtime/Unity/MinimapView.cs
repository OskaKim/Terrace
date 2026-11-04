using System.Collections.Generic;
using Terrace.Client.Core;
using Terrace.Client.Presentation;
using Terrace.Map;
using UnityEngine;
using UnityEngine.UI;

namespace Terrace.Client.Unity
{
    /// <summary>ミニマップの数値と色。画素は 1280x720 を基準にした UI の画素。</summary>
    public sealed class MinimapConfig
    {
        /// <summary>自分と他のプレイヤーの点を動かす間隔(秒)。他のプレイヤーの移動の通知の間隔(MoveSender.MinInterval)と同じくらいにしておく。</summary>
        public float RefreshSeconds { get; set; } = 0.1f;

        /// <summary>1 ユニットあたりの画素数(大きすぎるマップでは縮める)。</summary>
        public float PixelsPerUnit { get; set; } = 5f;

        /// <summary>地図の部分の最大の幅と高さ。</summary>
        public float MaxWidth { get; set; } = 320f;
        public float MaxHeight { get; set; } = 160f;

        /// <summary>足場・はしご・ポータル・NPC を囲む範囲の外に足す余白(ユニット)。</summary>
        public float Margin { get; set; } = 2f;

        /// <summary>地形の絵の、UI の 1 画素あたりの画素数(大きい画面でぼやけないように)。</summary>
        public int TextureScale { get; set; } = 2;

        public float LineWidth { get; set; } = 2f;
        public float MarkerSize { get; set; } = 6f;
        public float PlayerDotSize { get; set; } = 6f;
        public float SelfDotSize { get; set; } = 8f;

        public Color Background { get; set; } = new Color(0f, 0f, 0f, 0.55f);
        public Color FootholdColor { get; set; } = new Color(0.85f, 0.85f, 0.85f);
        public Color LadderColor { get; set; } = new Color(0.72f, 0.5f, 0.28f);
        public Color PortalColor { get; set; } = new Color(0.35f, 0.65f, 1f);
        public Color NpcColor { get; set; } = new Color(1f, 0.86f, 0.2f);
        public Color OtherPlayerColor { get; set; } = new Color(1f, 0.3f, 0.3f);
        public Color SelfColor { get; set; } = new Color(0.45f, 1f, 0.45f);

        public static MinimapConfig Default => new MinimapConfig();
    }

    /// <summary>
    /// 右上のミニマップ(uGUI をコードで組み立てる)。見出しにマップ名、その下に地図。
    /// - 地形(足場・はしご)・ポータル・NPC は、マップに入ったとき(開始時と MapChanged)に 1 回だけ Texture2D に描く
    /// - 自分と他のプレイヤーの点は、毎フレームではなく RefreshSeconds ごとに動かす(メインスレッドで間引く)
    /// どこに何を描くかの計算は MinimapLayout。HUD と同じく MVP にせず、間隔ごとに状態を読んで描く。
    /// </summary>
    public sealed class MinimapView : MonoBehaviour
    {
        private const float Padding = 6f;
        private const float HeaderHeight = 20f;
        private const float ScreenMargin = 10f;

        private readonly Dictionary<int, Image> _remoteDots = new Dictionary<int, Image>();
        private readonly List<int> _gone = new List<int>();
        private readonly Vector3[] _corners = new Vector3[4];
        private GameSimulation _simulation = null!;
        private Camera _camera = null!;
        private MinimapConfig _config = null!;
        private UguiFactory _ui = null!;
        private Sprite _dot = null!;
        private RectTransform _panel = null!;
        private RectTransform _mapArea = null!;
        private RawImage _terrain = null!;
        private Text _header = null!;
        private Image _selfDot = null!;
        private Texture2D? _texture;
        private float _sinceRefresh;

        /// <summary>今のマップの描き方。</summary>
        public MinimapLayout? Layout { get; private set; }

        public bool IsVisible => _panel.gameObject.activeSelf;

        /// <summary>見出しに出しているマップ名。</summary>
        public string MapName => _header.text;

        /// <summary>他のプレイヤーの点の数。</summary>
        public int RemoteDotCount => _remoteDots.Count;

        /// <summary>自分の点の位置(地図の左下を原点にした画素)。</summary>
        public Vector2 SelfDotPosition => _selfDot.rectTransform.anchoredPosition;

        /// <summary>他のプレイヤーの点の位置(地図の左下を原点にした画素)。点が無ければ null。</summary>
        public Vector2? RemoteDotPosition(int playerId)
            => _remoteDots.TryGetValue(playerId, out var dot) ? dot.rectTransform.anchoredPosition : (Vector2?)null;

        /// <summary>地形を描いた絵。</summary>
        public Texture2D? TerrainTexture => _texture;

        /// <summary>ミニマップの下端の、画面の上からの位置(画素。IMGUI の座標)。HUD の右上の文字をこの下に出す。</summary>
        public float ScreenBottom
        {
            get
            {
                _panel.GetWorldCorners(_corners);
                var bottomLeft = RectTransformUtility.WorldToScreenPoint(_camera, _corners[0]);
                return Screen.height - bottomLeft.y;
            }
        }

        public static MinimapView Create(GameSimulation simulation, Camera camera, MinimapConfig? config = null)
        {
            // Screen Space - Camera にして、カメラの絵(スクリーンショット)にも写るようにする。店の窓(100)より下
            var canvasGo = UguiFactory.CreateCanvas("MinimapCanvas", camera, 90);
            var view = canvasGo.AddComponent<MinimapView>();
            view._simulation = simulation;
            view._camera = camera;
            view._config = config ?? MinimapConfig.Default;
            view._ui = new UguiFactory();
            view._dot = SpriteFactory.Circle();
            view.BuildHierarchy();
            view.Rebuild(simulation.Map);
            simulation.Travel.MapChanged += view.OnMapChanged;
            return view;
        }

        /// <summary>出す・隠すを切り替える。出したときはすぐ点を合わせる。</summary>
        public void Toggle() => SetVisible(!IsVisible);

        public void SetVisible(bool visible)
        {
            _panel.gameObject.SetActive(visible);
            if (visible) Refresh();
        }

        /// <summary>自分と他のプレイヤーの点を、今の位置に合わせる。</summary>
        public void Refresh()
        {
            _sinceRefresh = 0f;
            if (Layout == null) return;

            var motor = _simulation.Motor;
            Place(_selfDot, motor.X, motor.Y);

            _gone.Clear();
            _gone.AddRange(_remoteDots.Keys);
            foreach (var other in _simulation.RemotePlayers.All)
            {
                _gone.Remove(other.PlayerId);
                if (!_remoteDots.TryGetValue(other.PlayerId, out var dot))
                {
                    dot = NewDot($"Player {other.PlayerId}", _config.OtherPlayerColor, _config.PlayerDotSize);
                    _remoteDots.Add(other.PlayerId, dot);
                }
                Place(dot, other.X, other.Y);
            }
            foreach (var playerId in _gone)
            {
                Destroy(_remoteDots[playerId].gameObject);
                _remoteDots.Remove(playerId);
            }
            _selfDot.transform.SetAsLastSibling();
        }

        private void Update()
        {
            if (Layout == null || !IsVisible) return;
            _sinceRefresh += Time.unscaledDeltaTime;
            if (_sinceRefresh >= _config.RefreshSeconds) Refresh();
        }

        private void OnDestroy()
        {
            if (_simulation != null) _simulation.Travel.MapChanged -= OnMapChanged;
            if (_texture != null) Destroy(_texture);
        }

        private void OnMapChanged(MapData previous, MapData next) => Rebuild(next);

        private void BuildHierarchy()
        {
            var panel = _ui.NewImage("Panel", transform, null, _config.Background);
            panel.raycastTarget = false;
            _panel = panel.rectTransform;
            _panel.anchorMin = new Vector2(1f, 1f);
            _panel.anchorMax = new Vector2(1f, 1f);
            _panel.pivot = new Vector2(1f, 1f);
            _panel.anchoredPosition = new Vector2(-ScreenMargin, -ScreenMargin);

            _header = _ui.NewText("MapName", _panel, string.Empty, 14, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
            _header.rectTransform.anchorMin = new Vector2(0f, 1f);
            _header.rectTransform.anchorMax = new Vector2(1f, 1f);
            _header.rectTransform.pivot = new Vector2(0.5f, 1f);
            _header.rectTransform.offsetMin = new Vector2(Padding, -HeaderHeight - Padding * 0.5f);
            _header.rectTransform.offsetMax = new Vector2(-Padding, -Padding * 0.5f);

            var mapArea = new GameObject("Map", typeof(RectTransform));
            mapArea.transform.SetParent(_panel, false);
            _mapArea = (RectTransform)mapArea.transform;

            var terrainGo = new GameObject("Terrain", typeof(RectTransform));
            terrainGo.transform.SetParent(_mapArea, false);
            _terrain = terrainGo.AddComponent<RawImage>();
            _terrain.raycastTarget = false;
            UguiFactory.Stretch(_terrain.rectTransform);

            _selfDot = NewDot("Self", _config.SelfColor, _config.SelfDotSize);
        }

        /// <summary>マップに合わせて作り直す: 見出し、大きさ、地形の絵、点。</summary>
        private void Rebuild(MapData map)
        {
            var layout = MinimapLayout.Create(map, _config.Margin, _config.PixelsPerUnit, _config.MaxWidth, _config.MaxHeight);
            Layout = layout;
            _header.text = map.Name;
            _panel.sizeDelta = new Vector2(layout.Width + Padding * 2f, HeaderHeight + layout.Height + Padding * 2f);
            UguiFactory.At(_mapArea, Padding, HeaderHeight + Padding, layout.Width, layout.Height);
            DrawTerrain(layout);
            Refresh();
        }

        private void DrawTerrain(MinimapLayout layout)
        {
            var scale = Mathf.Max(1, _config.TextureScale);
            var width = Mathf.Max(1, Mathf.CeilToInt(layout.Width * scale));
            var height = Mathf.Max(1, Mathf.CeilToInt(layout.Height * scale));
            if (_texture == null || _texture.width != width || _texture.height != height)
            {
                if (_texture != null) Destroy(_texture);
                _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
                {
                    name = "minimap-terrain",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                };
            }

            var pixels = new Color32[width * height];
            var canvas = new PixelCanvas(pixels, width, height);
            var lineHalf = _config.LineWidth * scale * 0.5f;
            foreach (var line in layout.Lines)
            {
                var color = line.Kind == MinimapLineKind.Ladder ? _config.LadderColor : _config.FootholdColor;
                canvas.Line(line.X1 * scale, line.Y1 * scale, line.X2 * scale, line.Y2 * scale, lineHalf, color);
            }
            var markerRadius = _config.MarkerSize * scale * 0.5f;
            foreach (var marker in layout.Markers)
            {
                var color = marker.Kind == MinimapMarkerKind.Npc ? _config.NpcColor : _config.PortalColor;
                canvas.Disc(marker.X * scale, marker.Y * scale, markerRadius, color);
            }
            _texture.SetPixels32(pixels);
            _texture.Apply(false);
            _terrain.texture = _texture;
        }

        private Image NewDot(string name, Color color, float size)
        {
            var dot = _ui.NewImage(name, _mapArea, _dot, color);
            dot.raycastTarget = false;
            var rect = dot.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            return dot;
        }

        private void Place(Image dot, float worldX, float worldY)
        {
            var (x, y) = Layout!.ToMinimap(worldX, worldY);
            dot.rectTransform.anchoredPosition = new Vector2(x, y);
        }

        /// <summary>画素の配列に線と円を塗る(左下が原点)。</summary>
        private readonly struct PixelCanvas
        {
            private readonly Color32[] _pixels;
            private readonly int _width;
            private readonly int _height;

            public PixelCanvas(Color32[] pixels, int width, int height)
            {
                _pixels = pixels;
                _width = width;
                _height = height;
            }

            /// <summary>太さ halfWidth × 2 の線(端点のあいだに四角を並べて押す)。</summary>
            public void Line(float x1, float y1, float x2, float y2, float halfWidth, Color32 color)
            {
                var steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(Mathf.Abs(x2 - x1), Mathf.Abs(y2 - y1))));
                for (var i = 0; i <= steps; i++)
                {
                    var t = i / (float)steps;
                    Square(x1 + (x2 - x1) * t, y1 + (y2 - y1) * t, halfWidth, color);
                }
            }

            public void Square(float cx, float cy, float half, Color32 color)
            {
                var minX = Mathf.Max(0, Mathf.FloorToInt(cx - half));
                var maxX = Mathf.Min(_width - 1, Mathf.CeilToInt(cx + half) - 1);
                var minY = Mathf.Max(0, Mathf.FloorToInt(cy - half));
                var maxY = Mathf.Min(_height - 1, Mathf.CeilToInt(cy + half) - 1);
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++) _pixels[y * _width + x] = color;
                }
            }

            public void Disc(float cx, float cy, float radius, Color32 color)
            {
                var minX = Mathf.Max(0, Mathf.FloorToInt(cx - radius));
                var maxX = Mathf.Min(_width - 1, Mathf.CeilToInt(cx + radius));
                var minY = Mathf.Max(0, Mathf.FloorToInt(cy - radius));
                var maxY = Mathf.Min(_height - 1, Mathf.CeilToInt(cy + radius));
                var squared = radius * radius;
                for (var y = minY; y <= maxY; y++)
                {
                    for (var x = minX; x <= maxX; x++)
                    {
                        var dx = x + 0.5f - cx;
                        var dy = y + 0.5f - cy;
                        if (dx * dx + dy * dy <= squared) _pixels[y * _width + x] = color;
                    }
                }
            }
        }
    }
}
