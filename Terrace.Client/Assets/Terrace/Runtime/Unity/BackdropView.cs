using System.Collections.Generic;
using Terrace.Client.Presentation;
using Terrace.Map;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// 背景の層(マップの backgrounds、無ければテーマの既定の組)を描く。層ごとに視差で動かし、TileX の層は横に並べて画面を覆う。
    /// 位置はカメラが動いた後(<see cref="CameraRig.Moved"/>)に決める。LateUpdate どうしの順番に頼ると、1 フレーム前のカメラ位置で置いて震えるため。
    /// 位置の計算は <see cref="BackdropLayout"/>。
    /// </summary>
    public sealed class BackdropView : MonoBehaviour
    {
        private readonly List<LayerRenderers> _layers = new List<LayerRenderers>();
        private Camera? _camera;
        private CameraRig? _rig;
        private float _centerX;
        private float _centerY;
        private float _startTime;

        /// <summary>描いている層の数(絵の見つからない層は除く)。</summary>
        public int LayerCount => _layers.Count;

        /// <summary>最後に位置を決めたときの、流れる層の経過秒。</summary>
        public float ScrollTime { get; private set; }

        /// <summary>位置の基準にしているマップの中心。</summary>
        public Vector2 MapCenter => new Vector2(_centerX, _centerY);

        public BackgroundLayer LayerAt(int index) => _layers[index].Layer;

        /// <summary>最後に決めた層の位置(TileX の層では、番号 0 の絵の位置)。</summary>
        public Vector2 AnchorOf(int index) => _layers[index].Anchor;

        /// <summary>層の絵(TileX の層では、いま画面に掛かっている分だけが有効)。</summary>
        public IReadOnlyList<SpriteRenderer> RenderersOf(int index) => _layers[index].Renderers;

        public void Bind(Camera camera, CameraRig rig, MapData map, IReadOnlyList<BackgroundLayer> layers, ArtLibrary art)
        {
            _camera = camera;
            _rig = rig;
            var bounds = map.Bounds;
            if (bounds != null && bounds.IsValid)
            {
                _centerX = (bounds.Left + bounds.Right) * 0.5f;
                _centerY = (bounds.Bottom + bounds.Top) * 0.5f;
            }

            for (var i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                var sprite = art.Get(layer.Sprite);
                if (sprite == null)
                {
                    Debug.LogWarning($"[map] 背景の絵が見つかりません: {layer.Sprite}", this);
                    continue;
                }
                var root = new GameObject($"Background {i} {layer.Sprite}");
                root.transform.SetParent(transform, false);
                _layers.Add(new LayerRenderers(layer, sprite, root.transform));
            }

            _startTime = Time.time;
            rig.Moved += Follow;
            Follow();
        }

        private void OnDestroy()
        {
            if (_rig != null) _rig.Moved -= Follow;
        }

        /// <summary>今のカメラ位置に合わせて層を置く。</summary>
        public void Follow()
        {
            if (_camera == null) return;
            var position = _camera.transform.position;
            var halfViewWidth = _camera.orthographicSize * _camera.aspect;
            ScrollTime = Time.time - _startTime;
            foreach (var layer in _layers)
            {
                layer.Place(position.x, position.y, _centerX, _centerY, ScrollTime, halfViewWidth);
            }
        }

        /// <summary>1 層ぶんの絵。TileX なら画面に掛かる枚数だけ使い回す。</summary>
        private sealed class LayerRenderers
        {
            private readonly Sprite _sprite;
            private readonly Transform _root;
            private readonly float _scale;
            private readonly float _width;
            private readonly List<SpriteRenderer> _renderers = new List<SpriteRenderer>();

            public LayerRenderers(BackgroundLayer layer, Sprite sprite, Transform root)
            {
                Layer = layer;
                _sprite = sprite;
                _root = root;
                _scale = layer.Scale <= 0f ? 1f : layer.Scale;
                _width = sprite.bounds.size.x * _scale;
            }

            public BackgroundLayer Layer { get; }
            public Vector2 Anchor { get; private set; }
            public IReadOnlyList<SpriteRenderer> Renderers => _renderers;

            public void Place(float cameraX, float cameraY, float centerX, float centerY, float time, float halfViewWidth)
            {
                var x = BackdropLayout.AnchorX(Layer, centerX, cameraX, time);
                var y = BackdropLayout.AnchorY(Layer, centerY, cameraY);
                Anchor = new Vector2(x, y);

                if (!Layer.TileX)
                {
                    Ensure(1);
                    _renderers[0].transform.position = new Vector3(x, y, 0f);
                    return;
                }

                var period = BackdropLayout.Period(Layer, _width);
                var tiles = BackdropLayout.VisibleTiles(x, period, _width, cameraX, halfViewWidth);
                Ensure(tiles.Count);
                for (var i = 0; i < _renderers.Count; i++)
                {
                    var renderer = _renderers[i];
                    var visible = i < tiles.Count;
                    if (renderer.enabled != visible) renderer.enabled = visible;
                    if (visible) renderer.transform.position = new Vector3(x + (tiles.First + i) * period, y, 0f);
                }
            }

            private void Ensure(int count)
            {
                while (_renderers.Count < count)
                {
                    _renderers.Add(SpriteFactory.CreateRenderer($"Tile {_renderers.Count}", _root, _sprite, Color.white, _scale, _scale, Layer.Layer));
                }
            }
        }
    }
}
