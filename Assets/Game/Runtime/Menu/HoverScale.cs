using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.Runtime.Menu
{
    /// <summary>Agranda suavemente el objeto (botón) mientras el cursor está encima.</summary>
    [DisallowMultipleComponent]
    public sealed class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public float hoverScale = 1.08f;
        public float speed = 10f;
        public Transform target; // si se asigna, agranda ESTE en vez de sí mismo (p. ej. solo el libro del botón)
        public Graphic glow;     // opcional: se ilumina (fade de alfa) al pasar el cursor
        public float glowAlpha = 0.85f;
        public GoldParticles particles; // opcional: duplica las partículas al pasar el cursor
        public SignSwing swing;         // opcional: sacude (balanceo) al entrar el cursor

        private Vector3 _base = Vector3.one;
        private float _t;        // 0 = normal, 1 = hover
        private float _target;

        private Transform Node => target != null ? target : transform;

        private void Awake() => _base = Node.localScale;

        public void OnPointerEnter(PointerEventData _) { _target = 1f; if (particles != null) particles.SetBoost(true); if (swing != null) swing.Trigger(); }
        public void OnPointerExit(PointerEventData _) { _target = 0f; if (particles != null) particles.SetBoost(false); }

        private void Update()
        {
            if (Mathf.Approximately(_t, _target)) return;
            _t = Mathf.MoveTowards(_t, _target, speed * Time.unscaledDeltaTime);
            float k = Mathf.SmoothStep(1f, hoverScale, _t);
            Node.localScale = _base * k;
            if (glow != null) { var c = glow.color; c.a = Mathf.Lerp(0f, glowAlpha, _t); glow.color = c; }
        }
    }
}
