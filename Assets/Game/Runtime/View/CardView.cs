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

        private Transform _visual = null!;
        private Vector3 _basePos;
        private Quaternion _baseRot = Quaternion.identity;
        private bool _hovered;
        /// <summary>El jugador activo puede jugar esta carta (solo cartas de su mano).</summary>
        public bool Playable { get; set; }

        private static Shader? _unlit;
        private static bool _unlitTried;

        /// <summary>Shader URP Unlit (color exacto, sin iluminación). null si no está disponible.</summary>
        public static Shader? UnlitShader()
        {
            if (!_unlitTried)
            {
                _unlitTried = true;
                _unlit = Shader.Find("Universal Render Pipeline/Unlit");
            }
            return _unlit;
        }

        public static CardView Create(Transform parent)
        {
            // Root: queda en la posición base con el COLLIDER (área de clic fija). El hijo
            // "Visual" es el que se levanta/escala en hover, así el collider no se mueve y no
            // hay parpadeo de hover en los bordes.
            var go = new GameObject("Card");
            go.transform.SetParent(parent, false);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);

            // El cubo trae el material URP por defecto (válido). Lo tintamos en Bind;
            // NO lo reemplazamos por uno de Shader.Find (eso daba magenta bajo URP).
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(visual, false);
            cube.transform.localScale = new Vector3(1.4f, 0.05f, 2.0f); // carta acostada
            Object.Destroy(cube.GetComponent<BoxCollider>());

            var view = go.AddComponent<CardView>();
            view._renderer = cube.GetComponent<MeshRenderer>();
            view._visual = visual;

            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(1.4f, 0.2f, 2.0f);

            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(visual, false);
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

        public void Bind(CardInstance card, int ownerId, bool faceDown,
                         Texture2D? front = null, Texture2D? back = null, bool flipTexture = true)
        {
            Card = card;
            OwnerId = ownerId;
            FaceDown = faceDown;

            var mat = _renderer.material; // instancia propia para no pisar el shared
            var unlit = UnlitShader();
            if (unlit != null && mat.shader != unlit) mat.shader = unlit; // sin iluminación
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0f); // (si quedara Lit) mate
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
            var tex = faceDown ? back : front;

            if (tex != null)
            {
                // Carta con arte completo (marco+nombre+coste baked): textura blanca sin tinte.
                mat.color = Color.white;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                mat.mainTexture = tex;
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);

                // La cara superior del cubo mapea la textura "de cabeza". Las cartas sin giro
                // físico de 180° (jugador y campo) se corrigen rotando la textura 180°; las que
                // ya giran 180° (mano rival) no necesitan flip.
                var scale = flipTexture ? new Vector2(-1f, -1f) : Vector2.one;
                var offset = flipTexture ? new Vector2(1f, 1f) : Vector2.zero;
                mat.mainTextureScale = scale;
                mat.mainTextureOffset = offset;
                if (mat.HasProperty("_BaseMap"))
                {
                    mat.SetTextureScale("_BaseMap", scale);
                    mat.SetTextureOffset("_BaseMap", offset);
                }
                _label.gameObject.SetActive(false);
            }
            else
            {
                // Fallback: quad tintado por tipo + etiqueta de texto.
                var c = faceDown ? new Color(0.2f, 0.2f, 0.25f) : BoardLayout.ColorFor(card.Type);
                mat.color = c;
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
                mat.mainTexture = null;
                _label.text = faceDown ? "?" : Short(card);
                _label.gameObject.SetActive(true);
            }
        }

        /// <summary>Coloca la carta y guarda su pose base (para levantarla/escalarla en hover).</summary>
        public void Place(Vector3 pos) => Place(pos, Quaternion.identity);

        public void Place(Vector3 pos, Quaternion rot)
        {
            _basePos = pos;
            _baseRot = rot;
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
            // Root (collider) fijo en la base; el Visual se levanta/escala -> sin parpadeo.
            transform.position = _basePos;
            transform.rotation = _baseRot;
            transform.localScale = Vector3.one;

            float lift = (Playable ? 0.2f : 0f) + (_hovered ? 0.6f : 0f);
            _visual.localPosition = new Vector3(0f, lift, 0f);
            _visual.localScale = Vector3.one * (_hovered ? 1.1f : 1f);
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
