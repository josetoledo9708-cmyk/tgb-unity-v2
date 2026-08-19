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

        /// <summary>Engranaje (cog) blanco sobre transparente: cuerpo dentado + agujero central.
        /// Se tiñe con Image.color. Antialias 1px en bordes.</summary>
        public static Sprite Gear(int size = 72, int teeth = 8)
        {
            string key = $"gear_{size}_{teeth}";
            if (_cache.TryGetValue(key, out var s)) return s;

            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float c = size / 2f;
            float rOut = size * 0.47f;   // punta del diente
            float rBody = size * 0.36f;  // base del diente / borde del cuerpo
            float rHole = size * 0.15f;  // agujero central
            const float twoPi = Mathf.PI * 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - c, dy = y + 0.5f - c;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    float t = Mathf.Repeat(ang / twoPi * teeth, 1f); // posición dentro de un diente
                    float d2 = Mathf.Abs(t - 0.25f);                 // diente centrado en 0.25
                    float toothR;
                    if (d2 < 0.16f) toothR = rOut;                                   // meseta del diente
                    else if (d2 < 0.24f) toothR = Mathf.Lerp(rOut, rBody, (d2 - 0.16f) / 0.08f); // flanco
                    else toothR = rBody;                                             // hueco
                    float aOuter = Mathf.Clamp01(toothR - r + 0.5f);
                    float aInner = Mathf.Clamp01(r - rHole + 0.5f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Min(aOuter, aInner)));
                }
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            s = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            _cache[key] = s;
            return s;
        }

        /// <summary>Degradado horizontal (izq→der), N×1, para oscurecer un lado (scrim de legibilidad).</summary>
        public static Sprite HGradient(Color left, Color right, int width = 64)
        {
            string key = $"hgrad_{left}_{right}_{width}";
            if (_cache.TryGetValue(key, out var s)) return s;
            var tex = new Texture2D(width, 1, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int x = 0; x < width; x++)
                tex.SetPixel(x, 0, Color.Lerp(left, right, x / (float)(width - 1)));
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            s = Sprite.Create(tex, new Rect(0, 0, width, 1), new Vector2(0.5f, 0.5f));
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
