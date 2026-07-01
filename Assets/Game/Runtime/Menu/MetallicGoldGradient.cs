using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Menu
{
    /// <summary>
    /// Efecto de malla que tiñe cualquier Graphic (Image de borde o Text) con la textura
    /// "dorao.png" (oro metalizado real), muestreada por la posición local del vértice. Si la
    /// textura no está disponible, cae a un degradado procedural de oro metalizado.
    /// Multiplica sobre el color existente para respetar alpha y el tinte de hover del botón.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MetallicGoldGradient : BaseMeshEffect
    {
        private static Texture2D _goldTex;
        private static bool _goldTried;

        /// <summary>Textura dorao.png cacheada (null si no está disponible/legible).</summary>
        public static Texture2D GoldTexture()
        {
            if (_goldTried) return _goldTex;
            _goldTried = true;
            var sprite = MenuAssets.Sprite("dorao");
            _goldTex = sprite != null && sprite.texture.isReadable ? sprite.texture : null;
            return _goldTex;
        }

        // Paradas de 0 (abajo) a 1 (arriba): base → veta oscura → brillo → oro → borde superior.
        private static readonly (float t, Color c)[] Stops =
        {
            (0.00f, new Color(0.72f, 0.55f, 0.22f)),
            (0.42f, new Color(0.50f, 0.36f, 0.12f)),  // veta oscura
            (0.52f, new Color(1.00f, 0.92f, 0.60f)),  // brillo dorado (no blanco)
            (0.72f, new Color(0.95f, 0.85f, 0.52f)),  // ≈ oro del tema
            (1.00f, new Color(0.80f, 0.66f, 0.34f)),
        };

        private static Color Sample(float t)
        {
            for (int i = 1; i < Stops.Length; i++)
                if (t <= Stops[i].t)
                {
                    var a = Stops[i - 1];
                    var b = Stops[i];
                    return Color.Lerp(a.c, b.c, Mathf.InverseLerp(a.t, b.t, t));
                }
            return Stops[Stops.Length - 1].c;
        }

        private static readonly List<UIVertex> _verts = new();

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            vh.GetUIVertexStream(_verts);
            if (_verts.Count == 0) return;

            float minX = float.MaxValue, maxX = float.MinValue, minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < _verts.Count; i++)
            {
                var p = _verts[i].position;
                if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
                if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
            }
            float w = Mathf.Max(0.0001f, maxX - minX);
            float h = Mathf.Max(0.0001f, maxY - minY);
            var tex = GoldTexture();

            for (int i = 0; i < _verts.Count; i++)
            {
                var v = _verts[i];
                float u = (v.position.x - minX) / w;
                float t = (v.position.y - minY) / h;
                var g = tex != null ? tex.GetPixelBilinear(u, t) : Sample(t);
                v.color = new Color32(
                    (byte)(g.r * v.color.r), (byte)(g.g * v.color.g),
                    (byte)(g.b * v.color.b), v.color.a);
                _verts[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(_verts);
        }
    }
}
