using Terrace.Map;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>
    /// マップの静的な要素(足場・はしご・ロープ・ポータル・飾り・湧き点)を描く。背景の層と雲は BackdropView。
    /// 素材があれば Kenney のタイルをテーマに従って線分に沿って敷き詰め(斜面は回転)、無ければ LineRenderer と生成スプライト。
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        public static readonly Color[] LayerColors =
        {
            new Color(0.62f, 0.45f, 0.28f),
            new Color(0.36f, 0.62f, 0.36f),
            new Color(0.40f, 0.55f, 0.80f),
            new Color(0.70f, 0.50f, 0.75f),
        };

        public void Build(MapData map, bool showSpawnMarkers, ArtLibrary? art)
        {
            var theme = art?.Theme(map.Theme);
            if (art != null && art.IsAvailable && theme?.Top != null)
            {
                BuildWithArt(map, art, theme);
            }
            else
            {
                BuildFallback(map);
            }

            if (showSpawnMarkers)
            {
                foreach (var spawnPoint in map.SpawnPoints)
                {
                    var marker = SpriteFactory.CreateRenderer($"Spawn {spawnPoint.Id}", transform, SpriteFactory.Diamond(), new Color(0.9f, 0.3f, 0.3f, 0.5f), 0.4f, 0.4f, -4);
                    marker.transform.position = new Vector3(spawnPoint.X, spawnPoint.Y, 0f);
                }
            }
        }

        private void BuildWithArt(MapData map, ArtLibrary art, ThemeArt theme)
        {
            var surfaceTop = WithTopPivot(theme.Top!);
            var fillTop = theme.Fill != null ? WithTopPivot(theme.Fill) : null;

            foreach (var foothold in map.Footholds)
            {
                // 左から右へ向く線分として扱う(定義の向きに依らず、絵の上面が上を向くように)
                var left = foothold.X1 <= foothold.X2 ? foothold.Start : foothold.End;
                var right = foothold.X1 <= foothold.X2 ? foothold.End : foothold.Start;
                var dx = right.X - left.X;
                var dy = right.Y - left.Y;
                var length = Mathf.Sqrt(dx * dx + dy * dy);
                if (length <= 0.001f) continue;
                var angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;

                var go = new GameObject($"Foothold {foothold.Id}");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3((left.X + right.X) * 0.5f, (left.Y + right.Y) * 0.5f, 0f);
                go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

                var surface = go.AddComponent<SpriteRenderer>();
                surface.sprite = surfaceTop;
                surface.drawMode = SpriteDrawMode.Tiled;
                surface.tileMode = SpriteTileMode.Continuous;
                surface.size = new Vector2(length, 1f);
                surface.sortingOrder = 0;

                if (foothold.Layer == 0 && fillTop != null)
                {
                    var fillGo = new GameObject("Fill");
                    fillGo.transform.SetParent(go.transform, false);
                    fillGo.transform.localPosition = new Vector3(0f, -1f, 0f);
                    var fill = fillGo.AddComponent<SpriteRenderer>();
                    fill.sprite = fillTop;
                    fill.drawMode = SpriteDrawMode.Tiled;
                    fill.tileMode = SpriteTileMode.Continuous;
                    fill.size = new Vector2(length, 2f);
                    fill.sortingOrder = -1;
                }
            }

            var ladderSprite = art.Tile("ladder_mid");
            var ropeSprite = art.Tile("ropeVertical");
            foreach (var ladder in map.Ladders)
            {
                var sprite = ladder.IsRope ? ropeSprite ?? ladderSprite : ladderSprite ?? ropeSprite;
                if (sprite == null) continue;
                var go = new GameObject($"{(ladder.IsRope ? "Rope" : "Ladder")} {ladder.Id}");
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(ladder.X, ladder.Bottom, 0f);
                var renderer = go.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.tileMode = SpriteTileMode.Continuous;
                renderer.size = new Vector2(1f, ladder.Height);
                renderer.sortingOrder = -2;
            }

            var doorMid = art.Tile("door_openMid");
            var doorTop = art.Tile("door_openTop");
            foreach (var portal in map.Portals)
            {
                if (portal.IsSpawn) continue;
                if (doorMid == null)
                {
                    var circle = SpriteFactory.CreateRenderer($"Portal {portal.Name}", transform, SpriteFactory.Circle(), new Color(0.35f, 0.65f, 1f, 0.75f), 1.0f, 1.6f, -3);
                    circle.transform.position = new Vector3(portal.X, portal.Y, 0f);
                    continue;
                }
                var mid = SpriteFactory.CreateRenderer($"Portal {portal.Name}", transform, doorMid, Color.white, 1f, 1f, -3);
                mid.transform.position = new Vector3(portal.X, portal.Y, 0f);
                if (doorTop != null)
                {
                    var top = SpriteFactory.CreateRenderer("Top", mid.transform, doorTop, Color.white, 1f, 1f, -3);
                    top.transform.localPosition = new Vector3(0f, 1f, 0f);
                }
            }

            foreach (var decoration in map.Decorations)
            {
                var sprite = art.Get(decoration.Sprite);
                if (sprite == null)
                {
                    Debug.LogWarning($"[map] 飾りの絵が見つかりません: {decoration.Sprite}", this);
                    continue;
                }
                var scale = decoration.Scale <= 0f ? 1f : decoration.Scale;
                var renderer = SpriteFactory.CreateRenderer($"Decoration {decoration.Id} {decoration.Sprite}", transform, sprite, Color.white, decoration.FlipX ? -scale : scale, scale, decoration.Layer);
                renderer.transform.position = new Vector3(decoration.X, decoration.Y, 0f);
            }
        }

        private void BuildFallback(MapData map)
        {
            var lineMaterial = new Material(Shader.Find("Sprites/Default"));

            foreach (var foothold in map.Footholds)
            {
                var go = new GameObject($"Foothold {foothold.Id}");
                go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.SetPosition(0, new Vector3(foothold.X1, foothold.Y1, 0f));
                line.SetPosition(1, new Vector3(foothold.X2, foothold.Y2, 0f));
                line.startWidth = 0.18f;
                line.endWidth = 0.18f;
                line.numCapVertices = 4;
                line.material = lineMaterial;
                var color = LayerColors[Mathf.Abs(foothold.Layer) % LayerColors.Length];
                line.startColor = color;
                line.endColor = color;
                line.sortingOrder = 0;
            }

            foreach (var ladder in map.Ladders)
            {
                var color = ladder.IsRope ? new Color(0.75f, 0.60f, 0.35f) : new Color(0.70f, 0.70f, 0.75f);
                var renderer = SpriteFactory.CreateRenderer($"{(ladder.IsRope ? "Rope" : "Ladder")} {ladder.Id}", transform, SpriteFactory.Square(), color, ladder.IsRope ? 0.12f : 0.5f, ladder.Height, -2);
                renderer.transform.position = new Vector3(ladder.X, ladder.Bottom, 0f);
            }

            foreach (var portal in map.Portals)
            {
                if (portal.IsSpawn) continue;
                var renderer = SpriteFactory.CreateRenderer($"Portal {portal.Name}", transform, SpriteFactory.Circle(), new Color(0.35f, 0.65f, 1f, 0.75f), 1.0f, 1.6f, -3);
                renderer.transform.position = new Vector3(portal.X, portal.Y, 0f);
            }
        }

        /// <summary>上辺中央を原点にした複製(足場の線に上面を合わせるため)。Tiled 描画用に FullRect。</summary>
        private static Sprite WithTopPivot(Sprite source)
        {
            var sprite = Sprite.Create(source.texture, source.rect, new Vector2(0.5f, 1f), source.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = source.name + "-top";
            return sprite;
        }
    }
}
