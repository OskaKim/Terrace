using Terrace.Map;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>マップの静的な要素(足場・はしご・ポータル・湧き点)を描く。</summary>
    public sealed class MapView : MonoBehaviour
    {
        public static readonly Color[] LayerColors =
        {
            new Color(0.62f, 0.45f, 0.28f),
            new Color(0.36f, 0.62f, 0.36f),
            new Color(0.40f, 0.55f, 0.80f),
            new Color(0.70f, 0.50f, 0.75f),
        };

        public void Build(MapData map, bool showSpawnMarkers)
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

                if (!ladder.IsRope)
                {
                    var rungs = Mathf.Max(1, Mathf.FloorToInt(ladder.Height / 0.6f));
                    for (var i = 0; i < rungs; i++)
                    {
                        var rung = SpriteFactory.CreateRenderer("Rung", renderer.transform, SpriteFactory.Square(), new Color(0.45f, 0.45f, 0.5f), 1f, 0.08f / Mathf.Max(ladder.Height, 0.01f), -1);
                        rung.transform.localPosition = new Vector3(0f, (i + 0.5f) / rungs, 0f);
                        rung.transform.localScale = new Vector3(1f, 0.08f / Mathf.Max(ladder.Height, 0.01f), 1f);
                    }
                }
            }

            foreach (var portal in map.Portals)
            {
                var renderer = SpriteFactory.CreateRenderer($"Portal {portal.Name}", transform, SpriteFactory.Circle(), new Color(0.35f, 0.65f, 1f, 0.75f), 1.0f, 1.6f, -3);
                renderer.transform.position = new Vector3(portal.X, portal.Y, 0f);
            }

            if (showSpawnMarkers)
            {
                foreach (var spawnPoint in map.SpawnPoints)
                {
                    var renderer = SpriteFactory.CreateRenderer($"Spawn {spawnPoint.Id}", transform, SpriteFactory.Diamond(), new Color(0.9f, 0.3f, 0.3f, 0.5f), 0.4f, 0.4f, -4);
                    renderer.transform.position = new Vector3(spawnPoint.X, spawnPoint.Y, 0f);
                }
            }
        }
    }
}
