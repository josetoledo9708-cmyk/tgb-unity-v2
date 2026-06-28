using UnityEngine;
using Game.Core.Model;

namespace Game.Runtime.View
{
    /// <summary>
    /// Representación 3D mínima de una carta: un quad coloreado por tipo + una etiqueta de texto.
    /// Generada por código (sin prefab ni arte) para tener algo visible de inmediato.
    /// </summary>
    public sealed class CardView : MonoBehaviour
    {
        public CardInstance Card { get; private set; } = null!;
        public int OwnerId { get; private set; }
        public bool FaceDown { get; private set; }

        private TextMesh _label = null!;
        private MeshRenderer _renderer = null!;

        private Vector3 _basePos;
        private bool _hovered;
        /// <summary>El jugador activo puede jugar esta carta (solo cartas de su mano).</summary>
        public bool Playable { get; set; }

        public static CardView Create(Transform parent)
        {
            // Root SIN escala (para no deformar la etiqueta). La escala de carta va en el cubo.
            var go = new GameObject("Card");
            go.transform.SetParent(parent, false);

            // El cubo trae el material URP por defecto (válido). Lo tintamos en Bind;
            // NO lo reemplazamos por uno de Shader.Find (eso daba magenta bajo URP).
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(go.transform, false);
            cube.transform.localScale = new Vector3(1.4f, 0.05f, 2.0f); // carta acostada
            Object.Destroy(cube.GetComponent<BoxCollider>());

            var view = go.AddComponent<CardView>();
            view._renderer = cube.GetComponent<MeshRenderer>();

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(1.4f, 0.2f, 2.0f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.06f, 0f);
            labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            view._label = labelGo.AddComponent<TextMesh>();
            view._label.anchor = TextAnchor.MiddleCenter;
            view._label.alignment = TextAlignment.Center;
            view._label.fontSize = 40;
            view._label.characterSize = 0.085f;
            view._label.color = Color.black;
            return view;
        }

        public void Bind(CardInstance card, int ownerId, bool faceDown)
        {
            Card = card;
            OwnerId = ownerId;
            FaceDown = faceDown;
            var c = faceDown ? new Color(0.2f, 0.2f, 0.25f) : BoardLayout.ColorFor(card.Type);
            var mat = _renderer.material; // instancia propia para no pisar el shared
            mat.color = c;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c); // URP
            _label.text = faceDown ? "?" : Short(card);
            _label.gameObject.SetActive(true);
        }

        /// <summary>Coloca la carta y guarda su posición base (para levantarla en hover).</summary>
        public void Place(Vector3 pos)
        {
            _basePos = pos;
            ApplyTransform();
        }

        public void SetHovered(bool hovered)
        {
            if (_hovered == hovered) return;
            _hovered = hovered;
            ApplyTransform();
        }

        private void ApplyTransform()
        {
            float lift = (Playable ? 0.2f : 0f) + (_hovered ? 0.6f : 0f);
            transform.position = _basePos + Vector3.up * lift;
            transform.localScale = Vector3.one * (_hovered ? 1.1f : 1f);
        }

        private static string Short(CardInstance c)
        {
            string n = c.Nombre.Length > 11 ? c.Nombre.Substring(0, 10) + "…" : c.Nombre;
            if (CardTypeNames.IsSer(c.Type)) return $"{n}\n(d{c.DurLeft})";
            if (c.Type == CardType.Tierra) return n + (c.Tapped ? "\n[tap]" : "");
            return n;
        }
    }
}
