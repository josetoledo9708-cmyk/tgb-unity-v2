using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Runtime.Menu
{
    /// <summary>Distingue click izquierdo/derecho (y mantener pulsado = derecho, para móvil) sobre
    /// cualquier Graphic con raycast. Usado por las fichas de carta del constructor de mazos.</summary>
    public sealed class PointerClicks : MonoBehaviour,
        IPointerClickHandler, IPointerDownHandler, IPointerUpHandler
    {
        public System.Action onLeft;
        public System.Action onRight;
        public float longPress = 0.45f;

        private float _downT = -1f;
        private bool _longFired;

        public void OnPointerDown(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) { _downT = Time.unscaledTime; _longFired = false; }
        }

        private void Update()
        {
            if (_downT >= 0f && !_longFired && Time.unscaledTime - _downT >= longPress)
            {
                _longFired = true;               // mantener pulsado = derecho (móvil)
                onRight?.Invoke();
            }
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left) _downT = -1f;
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Right) { onRight?.Invoke(); return; }
            if (e.button == PointerEventData.InputButton.Left && !_longFired) onLeft?.Invoke();
        }
    }
}
