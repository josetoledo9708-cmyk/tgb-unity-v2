using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Game.Runtime.Menu
{
    /// <summary>Carrusel horizontal infinito de tomos. El elemento más cercano al centro es el
    /// seleccionado (se agranda). Se arrastra a los lados y hace snap al soltar; envuelve sin fin.</summary>
    [DisallowMultipleComponent]
    public sealed class TomoCarousel : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public struct Item { public Sprite sprite; public bool unlocked; public string lockedText; }

        public Action<int> onSelect;

        private RectTransform _content;
        private readonly List<RectTransform> _cells = new();
        private readonly List<Item> _items = new();
        private float _step = 210f;
        private float _pos;      // desplazamiento continuo (en unidades de item)
        private float _target;
        private bool _dragging;
        private int _selected = -1;

        public int Selected => _selected;
        public bool SelectedUnlocked => _selected >= 0 && _selected < _items.Count && _items[_selected].unlocked;

        /// <summary>Muestra/oculta la miniatura del tomo de una celda (p. ej. al quedarse sin tomos).</summary>
        public void SetThumbVisible(int itemIndex, bool visible)
        {
            if (itemIndex < 0 || itemIndex >= _cells.Count) return;
            var t = _cells[itemIndex].Find("Thumb");
            if (t != null) t.gameObject.SetActive(visible);
        }

        public void Build(RectTransform content, float step, IList<Item> items)
        {
            _content = content;
            _step = step;
            _items.Clear();
            _items.AddRange(items);
            for (int i = 0; i < _items.Count; i++)
            {
                var cell = new GameObject("Cell" + i, typeof(RectTransform), typeof(CanvasGroup));
                cell.transform.SetParent(_content, false);
                var crt = (RectTransform)cell.transform;
                crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f); crt.pivot = new Vector2(0.5f, 0.5f);
                crt.sizeDelta = new Vector2(210f, 150f);

                var it = _items[i];
                if (it.sprite != null)
                {
                    var th = new GameObject("Thumb", typeof(RectTransform), typeof(Image));
                    th.transform.SetParent(cell.transform, false);
                    var ti = th.GetComponent<Image>();
                    ti.sprite = it.sprite; ti.preserveAspect = true; ti.raycastTarget = false;
                    var thr = (RectTransform)th.transform;
                    thr.anchorMin = thr.anchorMax = new Vector2(0.5f, 0.5f); thr.pivot = new Vector2(0.5f, 0.5f);
                    thr.sizeDelta = new Vector2(150f, 146f);
                }
                else
                {
                    var lbl = MenuTheme.Label(cell.transform, it.lockedText ?? "Próximamente", 14,
                        new Color(0.7f, 0.68f, 0.6f), TextAnchor.MiddleCenter, FontStyle.Bold);
                    lbl.raycastTarget = false; MenuTheme.Stretch((RectTransform)lbl.transform);
                }
                _cells.Add(crt);
            }
            _pos = 0f; _target = 0f;
            Layout();
            UpdateSelection(true);
        }

        private int N => _items.Count;

        private void Layout()
        {
            if (N == 0) return;
            for (int i = 0; i < N; i++)
            {
                float off = Mathf.Repeat(i - _pos + N * 0.5f, N) - N * 0.5f; // envuelve a [-N/2, N/2)
                var c = _cells[i];
                c.anchoredPosition = new Vector2(off * _step, 0f);
                float d = Mathf.Clamp01(Mathf.Abs(off));
                float sc = Mathf.Lerp(1.16f, 0.82f, d);         // centro grande, lados chicos
                c.localScale = new Vector3(sc, sc, 1f);
                var cg = c.GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = Mathf.Lerp(1f, 0.4f, d);
            }
            // el más centrado, al frente
            int center = ((Mathf.RoundToInt(_pos) % N) + N) % N;
            _cells[center].SetAsLastSibling();
        }

        private void UpdateSelection(bool force)
        {
            if (N == 0) return;
            int sel = ((Mathf.RoundToInt(_pos) % N) + N) % N;
            if (sel != _selected || force)
            {
                _selected = sel;
                onSelect?.Invoke(sel);
            }
        }

        private void Update()
        {
            if (_dragging || N == 0) return;
            if (Mathf.Abs(_pos - _target) > 0.0005f)
            {
                _pos = Mathf.Lerp(_pos, _target, 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime));
                if (Mathf.Abs(_pos - _target) < 0.01f) _pos = _target;
                Layout();
                UpdateSelection(false);
            }
        }

        public void OnBeginDrag(PointerEventData e) => _dragging = true;

        public void OnDrag(PointerEventData e)
        {
            if (N == 0) return;
            _pos -= e.delta.x / _step;
            Layout();
            UpdateSelection(false);
        }

        public void OnEndDrag(PointerEventData e)
        {
            _dragging = false;
            _target = Mathf.Round(_pos);
        }
    }
}
