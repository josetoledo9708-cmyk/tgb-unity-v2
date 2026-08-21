using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Menu
{
    /// <summary>Cortina negra a pantalla completa: al llamar Play() aparece opaca y se desvanece,
    /// dando tiempo a que la nueva pantalla (p. ej. el fondo en video) cargue sin verse el salto.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Image))]
    public sealed class ScreenFader : MonoBehaviour
    {
        private Image _img;
        private float _t = -1f;
        private float _dur = 0.4f;

        private void Awake() => _img = GetComponent<Image>();

        public void Play(float duration)
        {
            if (_img == null) _img = GetComponent<Image>();
            _dur = Mathf.Max(0.01f, duration);
            _t = 0f;
            SetAlpha(1f);
            _img.raycastTarget = true; // bloquea input mientras funde
        }

        private void SetAlpha(float a)
        {
            var c = _img.color; c.a = a; _img.color = c;
        }

        private void Update()
        {
            if (_t < 0f || _img == null) return;
            _t += Time.unscaledDeltaTime;
            float a = 1f - Mathf.Clamp01(_t / _dur);
            SetAlpha(a);
            if (a <= 0f) { _t = -1f; _img.raycastTarget = false; }
        }
    }
}
