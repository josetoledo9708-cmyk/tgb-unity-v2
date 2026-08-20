using UnityEngine;

namespace Game.Runtime.Menu
{
    /// <summary>Animación suave del libro de Tomos: leve flotación vertical + latido de escala.</summary>
    [DisallowMultipleComponent]
    public sealed class BookIdle : MonoBehaviour
    {
        private RectTransform _rt;
        private Vector2 _base;

        private void Awake()
        {
            _rt = (RectTransform)transform;
            _base = _rt.anchoredPosition;
        }

        private void Update()
        {
            float t = Time.unscaledTime;
            _rt.anchoredPosition = _base + new Vector2(0f, Mathf.Sin(t * 1.2f) * 8f);
            _rt.localScale = Vector3.one * (1f + Mathf.Sin(t * 0.9f) * 0.015f);
        }
    }
}
