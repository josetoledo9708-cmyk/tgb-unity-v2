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

        private TextMesh _label = null!;
        private MeshRenderer _renderer = null!;

        public static CardView Create(Transform parent, Material baseMaterial)
        {
            var go = new GameObject("Card");
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(1.4f, 0.05f, 2.0f); // carta acostada

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(go.transform, false);
            Object.Destroy(cube.GetComponent<BoxCollider>());

            var view = go.AddComponent<CardView>();
            view._renderer = cube.GetComponent<MeshRenderer>();
            view._renderer.sharedMaterial = baseMaterial;
            go.AddComponent<BoxCollider>().size = Vector3.one;

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            labelGo.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            labelGo.transform.localScale = new Vector3(0.12f, 0.08f, 0.12f);
            view._label = labelGo.AddComponent<TextMesh>();
            view._label.anchor = TextAnchor.MiddleCenter;
            view._label.alignment = TextAlignment.Center;
            view._label.fontSize = 48;
            view._label.color = Color.black;
            return view;
        }

        public void Bind(CardInstance card, int ownerId, bool faceDown)
        {
            Card = card;
            OwnerId = ownerId;
            var mat = _renderer.material; // instancia propia para no pisar el shared
            mat.color = faceDown ? new Color(0.2f, 0.2f, 0.25f) : BoardLayout.ColorFor(card.Type);
            _label.text = faceDown ? "?" : Short(card);
            _label.gameObject.SetActive(true);
        }

        private static string Short(CardInstance c)
        {
            string n = c.Nombre.Length > 14 ? c.Nombre.Substring(0, 13) + "…" : c.Nombre;
            if (CardTypeNames.IsSer(c.Type)) return $"{n}\n(d{c.DurLeft})";
            if (c.Type == CardType.Tierra) return n + (c.Tapped ? "\n[tap]" : "");
            return n;
        }
    }
}
