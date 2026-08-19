using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.Runtime.Menu
{
    /// <summary>
    /// Enciende el reflejo (glint) de las <see cref="MetallicGoldGradient"/> hijas SOLO cuando el
    /// botón tiene el cursor encima o está seleccionado (navegación por teclado/mando). Va en el
    /// GameObject del botón; el texto (con el glint) es hijo y no recibe raycast.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GlintOnHover : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        private MetallicGoldGradient[] _targets;
        private bool _hover, _selected;

        private MetallicGoldGradient[] Targets =>
            _targets ??= GetComponentsInChildren<MetallicGoldGradient>(true);

        private void Apply()
        {
            bool on = _hover || _selected;
            var ts = Targets;
            for (int i = 0; i < ts.Length; i++)
                if (ts[i] != null) ts[i].SetGlint(on);
        }

        public void OnPointerEnter(PointerEventData e) { _hover = true; Apply(); }
        public void OnPointerExit(PointerEventData e)  { _hover = false; Apply(); }
        public void OnSelect(BaseEventData e)          { _selected = true; Apply(); }
        public void OnDeselect(BaseEventData e)        { _selected = false; Apply(); }

        private void OnDisable() { _hover = false; _selected = false; Apply(); }
    }
}
