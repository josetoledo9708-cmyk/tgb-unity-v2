using UnityEngine;

namespace Game.Runtime.Menu
{
    /// <summary>Balanceo tipo péndulo (rotación Z amortiguada) alrededor del pivote del objeto,
    /// como un letrero colgante. Llamar Trigger() para sacudirlo.</summary>
    [DisallowMultipleComponent]
    public sealed class SignSwing : MonoBehaviour
    {
        public float amplitude = 9f;   // grados iniciales
        public float freq = 2.2f;      // oscilaciones por segundo
        public float damp = 3.2f;      // amortiguación

        private RectTransform _rt;
        private float _t = -1f;        // <0 = en reposo

        private void Awake() => _rt = (RectTransform)transform;

        public void Trigger() => _t = 0f;

        private void Update()
        {
            if (_t < 0f) return;
            _t += Time.unscaledDeltaTime;
            float a = amplitude * Mathf.Exp(-damp * _t) * Mathf.Sin(2f * Mathf.PI * freq * _t);
            _rt.localEulerAngles = new Vector3(0f, 0f, a);
            if (_t > 6f / damp) { _t = -1f; _rt.localEulerAngles = Vector3.zero; } // ya casi quieto
        }
    }
}
