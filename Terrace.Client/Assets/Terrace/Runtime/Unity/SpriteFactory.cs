using System;
using System.Collections.Generic;
using UnityEngine;

namespace Terrace.Client.Unity
{
    /// <summary>アートアセット無しで動かすための、コード生成スプライト。すべて 1 ユニット幅、足元中央が原点。</summary>
    public static class SpriteFactory
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        public static Sprite Square() => Get("square", 8, 8, (x, y) => true);

        public static Sprite Circle() => Get("circle", 64, 64, (x, y) =>
        {
            var dx = (x + 0.5f) / 64f - 0.5f;
            var dy = (y + 0.5f) / 64f - 0.5f;
            return dx * dx + dy * dy <= 0.25f;
        });

        public static Sprite RoundedRect() => Get("rounded", 64, 64, (x, y) =>
        {
            const float radius = 14f;
            var px = x + 0.5f;
            var py = y + 0.5f;
            var cx = Mathf.Clamp(px, radius, 64f - radius);
            var cy = Mathf.Clamp(py, radius, 64f - radius);
            var dx = px - cx;
            var dy = py - cy;
            return dx * dx + dy * dy <= radius * radius;
        });

        public static Sprite Diamond() => Get("diamond", 32, 32, (x, y) =>
        {
            var dx = Math.Abs((x + 0.5f) / 32f - 0.5f);
            var dy = Math.Abs((y + 0.5f) / 32f - 0.5f);
            return dx + dy <= 0.5f;
        });

        public static SpriteRenderer CreateRenderer(string name, Transform parent, Sprite sprite, Color color, float width, float height, int sortingOrder)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(width, height, 1f);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = sortingOrder;
            return renderer;
        }

        private static Sprite Get(string key, int width, int height, Func<int, int, bool> fill)
        {
            if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = $"sprite-{key}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color32[width * height];
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    pixels[y * width + x] = fill(x, y) ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply();

            var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0f), width);
            sprite.name = key;
            Cache[key] = sprite;
            return sprite;
        }
    }
}
