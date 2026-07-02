using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Menu
{
    /// <summary>
    /// Efecto de malla que aplica un degradado vertical de ORO METALIZADO (banda de brillo + veta
    /// oscura) a cualquier Graphic (Image de borde o Text). Multiplica sobre el color existente para
    /// respetar alpha y el tinte de hover del botón.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MetallicGoldGradient : BaseMeshEffect
    {
        // Paradas de 0 (abajo) a 1 (arriba), centradas en BC8041 (0.737,0.502,0.255):
        // borde oscuro → veta oscura → brillo metálico → BC8041 exacto → borde superior oscuro.
        private static readonly (float t, Color c)[] Stops =
        {
            (0.00f, new Color(0.68f, 0.47f, 0.24f)),
            (0.36f, new Color(0.42f, 0.29f, 0.14f)),  // veta oscura
            (0.50f, new Color(1.15f, 1.08f, 0.85f)),  // brillo metálico (sobreexpuesto)
            (0.62f, new Color(1.00f, 0.90f, 0.62f)),  // realce entre brillo y BC8041
            (0.80f, new Color(0.737f, 0.502f, 0.255f)), // BC8041 exacto
            (1.00f, new Color(0.60f, 0.41f, 0.20f)),
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

            float minY = float.MaxValue, maxY = float.MinValue;
            for (int i = 0; i < _verts.Count; i++)
            {
                float y = _verts[i].position.y;
                if (y < minY) minY = y;
                if (y > maxY) maxY = y;
            }
            float h = Mathf.Max(0.0001f, maxY - minY);

            for (int i = 0; i < _verts.Count; i++)
            {
                var v = _verts[i];
                var g = Sample((v.position.y - minY) / h);
                v.color = new Color32(
                    (byte)Mathf.Clamp(g.r * v.color.r, 0f, 255f),
                    (byte)Mathf.Clamp(g.g * v.color.g, 0f, 255f),
                    (byte)Mathf.Clamp(g.b * v.color.b, 0f, 255f),
                    v.color.a);
                _verts[i] = v;
            }
            vh.Clear();
            vh.AddUIVertexTriangleStream(_verts);
        }
    }
}
