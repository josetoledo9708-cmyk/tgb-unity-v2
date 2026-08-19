using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Menu
{
    /// <summary>
    /// Efecto de malla que pinta cualquier Graphic (Text/Image) como ORO PULIDO: degradado vertical
    /// metálico (veta oscura → banda especular → oro) MÁS un reflejo de luz que barre en horizontal
    /// de forma periódica (glint). Multiplica sobre el color existente para respetar alpha y el tinte
    /// de hover del botón.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MetallicGoldGradient : BaseMeshEffect
    {
        [Header("Reflejo animado (glint)")]
        [Tooltip("Activa el reflejo de luz que barre el texto de izquierda a derecha.")]
        public bool animateGlint = true;
        [Tooltip("Segundos entre cada pasada del reflejo.")]
        public float glintPeriod = 3.6f;
        [Tooltip("Fracción del periodo que dura la pasada (resto: sin reflejo).")]
        public float glintSweep = 0.30f;
        [Tooltip("Intensidad del reflejo (0 = nada).")]
        public float glintStrength = 0.9f;
        [Tooltip("Ancho de la banda de reflejo (en fracción del ancho del texto).")]
        public float glintWidth = 0.11f;
        [Tooltip("Desfase inicial en segundos (para que no barran todos a la vez).")]
        public float glintOffset = 0f;

        // Paradas de 0 (abajo) a 1 (arriba): bronce profundo → oro → banda especular (sostenida) →
        // oro → oro superior. Más contraste/saturación que antes para que lea como oro brillante.
        private static readonly (float t, Color c)[] Stops =
        {
            (0.00f, new Color(0.55f, 0.36f, 0.14f)),   // bronce profundo (abajo)
            (0.30f, new Color(0.86f, 0.63f, 0.27f)),   // oro
            (0.45f, new Color(1.35f, 1.22f, 0.82f)),   // especular (sobreexpuesto)
            (0.55f, new Color(1.35f, 1.22f, 0.82f)),   // banda especular sostenida (raya nítida)
            (0.70f, new Color(0.98f, 0.74f, 0.33f)),   // oro
            (1.00f, new Color(0.72f, 0.51f, 0.22f)),   // oro superior
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

        // Posición del centro del reflejo (nx 0..1); >1 o <0 = fuera del texto (sin reflejo).
        private float GlintCenter()
        {
            if (!animateGlint || glintStrength <= 0f) return 99f;
            float period = Mathf.Max(0.05f, glintPeriod);
            float phase = Mathf.Repeat(Time.time + glintOffset, period) / period;
            float sweep = Mathf.Clamp01(glintSweep);
            if (phase >= sweep) return 99f;                       // pausa entre pasadas
            return Mathf.Lerp(-0.15f, 1.15f, phase / sweep);      // barre izq→der
        }

        private static readonly List<UIVertex> _verts = new();

        protected override void Start()
        {
            base.Start();
            // desfase aleatorio para que cada botón/etiqueta refleje en momentos distintos
            if (animateGlint && Mathf.Approximately(glintOffset, 0f))
                glintOffset = Random.value * Mathf.Max(0.05f, glintPeriod);
        }

        private void Update()
        {
            if (animateGlint && graphic != null) graphic.SetVerticesDirty(); // re-ejecuta ModifyMesh
        }

        public override void ModifyMesh(VertexHelper vh)
        {
            if (!IsActive()) return;
            vh.GetUIVertexStream(_verts);
            if (_verts.Count == 0) return;

            float minY = float.MaxValue, maxY = float.MinValue;
            float minX = float.MaxValue, maxX = float.MinValue;
            for (int i = 0; i < _verts.Count; i++)
            {
                var p = _verts[i].position;
                if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y;
                if (p.x < minX) minX = p.x; if (p.x > maxX) maxX = p.x;
            }
            float h = Mathf.Max(0.0001f, maxY - minY);
            float w = Mathf.Max(0.0001f, maxX - minX);

            float gc = GlintCenter();
            float sigma = Mathf.Max(0.02f, glintWidth);
            float twoSigma2 = 2f * sigma * sigma;

            for (int i = 0; i < _verts.Count; i++)
            {
                var v = _verts[i];
                var g = Sample((v.position.y - minY) / h);

                // reflejo horizontal: campana gaussiana centrada en gc, empuja hacia blanco.
                if (gc < 90f)
                {
                    float nx = (v.position.x - minX) / w;
                    float d = nx - gc;
                    float band = glintStrength * Mathf.Exp(-(d * d) / twoSigma2);
                    g = new Color(g.r + band, g.g + band, g.b + band);
                }

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
