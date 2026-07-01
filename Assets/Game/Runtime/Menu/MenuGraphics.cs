using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Menu
{
    /// <summary>Genera sprites por código (sin assets) para los diseños de UI: rect redondeado 9-slice,
    /// degradado vertical. Cacheado.</summary>
    public static class MenuGraphics
    {
        private static readonly Dictionary<string, Sprite> _cache = new();

        /// <summary>Rectángulo redondeado blanco (alpha), 9-slice con borde = radio.</summary>
        public static Sprite Rounded(int size = 64, int radius = 16)
        {
            string key = $"round_{size}_{radius}";
            if (_cache.TryGetValue(key, out var s)) return s;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float r = radius;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = 1f;
                    // centros de las 4 esquinas redondeadas
                    float cx = x < r ? r : (x > size - r ? size - r : x);
                    float cy = y < r ? r : (y > size - r ? size - r : y);
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d > r) a = Mathf.Clamp01(1f - (d - r));        // borde suave (antialias 1px)
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            _cache[key] = s;
            return s;
        }

        /// <summary>Degradado vertical (arriba→abajo), 1×N, para superponer un brillo.</summary>
        public static Sprite VGradient(Color top, Color bottom, int height = 64)
        {
            string key = $"vgrad_{top}_{bottom}_{height}";
            if (_cache.TryGetValue(key, out var s)) return s;
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < height; y++)
                tex.SetPixel(0, y, Color.Lerp(bottom, top, y / (float)(height - 1)));
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            s = Sprite.Create(tex, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f));
            _cache[key] = s;
            return s;
        }
    }
}
