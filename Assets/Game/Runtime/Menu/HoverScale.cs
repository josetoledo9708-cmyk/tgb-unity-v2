using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Runtime.Menu
{
    /// <summary>Agranda suavemente el objeto (botón) mientras el cursor está encima.</summary>
    [DisallowMultipleComponent]
    public sealed class HoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public float hoverScale = 1.08f;
        public float speed = 10f;
        public Transform target; // si se asigna, agranda ESTE en vez de sí mismo (p. ej. solo el libro del botón)

        private Vector3 _base = Vector3.one;
        private float _t;        // 0 = normal, 1 = hover
        private float _target;

        private Transform Node => target != null ? target : transform;

        private void Awake() => _base = Node.localScale;

        public void OnPointerEnter(PointerEventData _) => _target = 1f;
        public void OnPointerExit(PointerEventData _) => _target = 0f;

        private void Update()
        {
            if (Mathf.Approximately(_t, _target)) return;
            _t = Mathf.MoveTowards(_t, _target, speed * Time.unscaledDeltaTime);
            float k = Mathf.SmoothStep(1f, hoverScale, _t);
            Node.localScale = _base * k;
        }
    }
}
