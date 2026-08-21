using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Menu
{
    /// <summary>Da vida a un fondo estático: respiración de escala (leve zoom) y pulso de brillo,
    /// sincronizados en una onda lenta. Sin assets nuevos.</summary>
    [DisallowMultipleComponent]
    public sealed class BackgroundPulse : MonoBehaviour
    {
        public float scaleAmp = 0.045f;   // cuánto acerca (0..amp), siempre ≥1 para no descubrir bordes
        public float brightMin = 0.86f;   // brillo mínimo del pulso
        public float speed = 0.35f;       // rad/s de la onda (periodo ~18s)

        private RectTransform _rt;
        private Image _img;
        private Color _base;
        private float _t;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _img = GetComponent<Image>();
            if (_img != null) _base = _img.color;
        }

        private void Update()
        {
            _t += Time.unscaledDeltaTime * speed;
            float w = 0.5f + 0.5f * Mathf.Sin(_t);          // 0..1
            float s = 1f + scaleAmp * w;                    // 1 .. 1+amp
            _rt.localScale = new Vector3(s, s, 1f);
            if (_img != null)
            {
                float b = Mathf.Lerp(brightMin, 1f, w);
                _img.color = new Color(_base.r * b, _base.g * b, _base.b * b, _base.a);
            }
        }
    }
}
