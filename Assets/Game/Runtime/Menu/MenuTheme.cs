using System;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Menu
{
    /// <summary>
    /// Tema visual del menú (réplica del Godot: dorado sobre fondo oscuro, fuente Cinzel) + fábrica
    /// de widgets uGUI generados por código para no depender de prefabs.
    /// </summary>
    public static class MenuTheme
    {
        public static readonly Color Gold     = new(0.97f, 0.87f, 0.55f, 1f);
        public static readonly Color GoldDim  = new(0.65f, 0.55f, 0.28f, 1f);
        public static readonly Color MetalGold = new Color32(0xBC, 0x80, 0x41, 0xFF); // marco/texto de botones
        public static readonly Color DarkBg   = new(0.04f, 0.05f, 0.13f, 0.88f);
        public static readonly Color PanelBg  = new(0.06f, 0.05f, 0.02f, 0.92f);
        public static readonly Color HoverBg  = new(0.10f, 0.09f, 0.04f, 0.95f);

        // --- contenedores ---

        public static RectTransform Panel(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            Stretch(rt);
            return rt;
        }

        public static Image Rect(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            Stretch((RectTransform)go.transform);
            return img;
        }

        public static Image Picture(Transform parent, string name, Sprite sprite, bool preserveAspect = true)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = preserveAspect;
            img.color = sprite != null ? Color.white : new Color(1, 1, 1, 0f);
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color,
                                 TextAnchor anchor = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = MenuAssets.Font();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.fontStyle = style;
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Truncate;
            return t;
        }

        // --- botones ---

        /// <summary>Botón de texto con StyleBox dorado/oscuro (como el MainMenu de Godot).</summary>
        public static Button TextButton(Transform parent, string label, int size, Action onClick,
                                        float width = 320f, float height = 54f)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(width, height);
            var img = go.GetComponent<Image>();
            img.color = Color.black;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var cols = btn.colors;
            cols.normalColor = Color.white;
            cols.highlightedColor = new Color(1.3f, 1.3f, 1.3f, 1f);
            cols.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            cols.fadeDuration = 0.08f;
            btn.colors = cols;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var txt = Label(rt, label, Mathf.RoundToInt(size * 1.1f), Color.white, TextAnchor.MiddleCenter, FontStyle.Bold); // +10%
            ThickenText(txt);                                    // más espesor (faux-bold)
            txt.gameObject.AddComponent<MetallicGoldGradient>(); // mismo metálico que los botones principales
            Stretch((RectTransform)txt.transform);
            AddGoldBorder(rt);
            go.AddComponent<GlintOnHover>(); // reflejo solo con hover/selección
            return btn;
        }

        /// <summary>Botón hecho de una imagen (btn_historias.png, etc.).</summary>
        public static Button ImageButton(Transform parent, Sprite sprite, Action onClick,
                                         float width, float height)
        {
            var go = new GameObject("ImgBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(width, height);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.preserveAspect = true;
            img.color = sprite != null ? Color.white : GoldDim;
            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            var cols = btn.colors;
            cols.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            cols.pressedColor = new Color(0.85f, 0.85f, 0.85f, 1f);
            btn.colors = cols;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            return btn;
        }

        /// <summary>Botón usando el asset Botones.png (marco+interior+brillo baked) con icono y
        /// texto Cinzel superpuestos. Si el asset no está disponible, cae al diseño procedural
        /// (marco redondeado + interior oscuro + brillo, generados con las herramientas de Unity).</summary>
        public static Button DesignedButton(Transform parent, string label, Action onClick,
                                            float width = 330f, float height = 50f,
                                            Func<Transform, RectTransform> icon = null)
        {
            var root = new GameObject("DBtn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            root.transform.SetParent(parent, false);
            var rrt = (RectTransform)root.transform;
            rrt.sizeDelta = new Vector2(width, height);

            var frame = root.GetComponent<Image>();
            var botonesSprite = MenuAssets.Sprite("Botones");
            if (botonesSprite != null)
            {
                frame.sprite = botonesSprite;
                frame.type = Image.Type.Sliced;
                frame.color = Color.white; // asset ya trae el color/brillo baked
            }
            else
            {
                // Fallback procedural: marco redondeado + interior oscuro + brillo superior.
                frame.sprite = MenuGraphics.Rounded(64, 18);
                frame.type = Image.Type.Sliced;
                frame.color = Color.white;
                frame.gameObject.AddComponent<MetallicGoldGradient>();

                var inner = new GameObject("Inner", typeof(RectTransform), typeof(Image));
                inner.transform.SetParent(rrt, false);
                var innerImg = inner.GetComponent<Image>();
                innerImg.sprite = MenuGraphics.Rounded(64, 16);
                innerImg.type = Image.Type.Sliced;
                innerImg.color = Color.black;
                innerImg.raycastTarget = false;
                Anchor((RectTransform)inner.transform, Vector2.zero, Vector2.one, new Vector2(3f, 3f), new Vector2(-3f, -3f));

                var gloss = new GameObject("Gloss", typeof(RectTransform), typeof(Image));
                gloss.transform.SetParent(rrt, false);
                var gImg = gloss.GetComponent<Image>();
                gImg.sprite = MenuGraphics.VGradient(new Color(1f, 0.92f, 0.65f, 0.30f), new Color(1f, 1f, 1f, 0f));
                gImg.raycastTarget = false;
                Anchor((RectTransform)gloss.transform, Vector2.zero, Vector2.one, new Vector2(4f, 4f), new Vector2(-4f, -4f));
            }

            var btn = root.GetComponent<Button>();
            btn.targetGraphic = frame;
            var cols = btn.colors;
            cols.normalColor = Color.white;
            cols.highlightedColor = new Color(1.15f, 1.1f, 0.95f, 1f);
            cols.pressedColor = new Color(0.85f, 0.8f, 0.6f, 1f);
            cols.fadeDuration = 0.08f;
            btn.colors = cols;
            if (onClick != null) btn.onClick.AddListener(() => onClick());
            root.AddComponent<HoverScale>(); // agranda al pasar el cursor

            if (icon != null) icon(rrt);
            float leftPad = icon != null ? 46f : 16f;
            var t = Label(rrt, label, 22, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold); // +10%
            t.raycastTarget = false;
            ThickenText(t);                                    // más espesor (faux-bold)
            t.gameObject.AddComponent<MetallicGoldGradient>(); // texto: metálico centrado en BC8041
            Anchor((RectTransform)t.transform, Vector2.zero, Vector2.one, new Vector2(leftPad, 0f), new Vector2(-16f, 0f));
            root.AddComponent<GlintOnHover>(); // reflejo solo con hover/selección
            return btn;
        }

        private static void AddGoldBorder(RectTransform target)
        {
            var outline = target.gameObject.AddComponent<Outline>();
            outline.effectColor = GoldDim;
            outline.effectDistance = new Vector2(2f, -2f);
        }

        /// <summary>Engrosa un Text (faux-bold): Outline blanco que duplica el glifo en las 4
        /// diagonales. Se añade ANTES del MetallicGoldGradient para que las copias también reciban
        /// el degradado dorado (letra más gruesa, mismo oro).</summary>
        private static void ThickenText(Text t)
        {
            var ol = t.gameObject.AddComponent<Outline>();
            ol.effectColor = Color.white;               // el degradado lo vuelve oro
            ol.effectDistance = new Vector2(0.3f, 0.3f);
            ol.useGraphicAlpha = true;
        }

        // --- layout helpers ---

        public static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        public static void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
                                  Vector2 offsetMin, Vector2 offsetMax)
        {
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
        }

        public static VerticalLayoutGroup VBox(Transform parent, float spacing, int pad = 0,
                                               TextAnchor align = TextAnchor.UpperCenter)
        {
            var go = new GameObject("VBox", typeof(RectTransform), typeof(VerticalLayoutGroup));
            go.transform.SetParent(parent, false);
            var v = go.GetComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.childAlignment = align;
            v.childForceExpandWidth = false;
            v.childForceExpandHeight = false;
            v.childControlWidth = false;
            v.childControlHeight = false;
            v.padding = new RectOffset(pad, pad, pad, pad);
            return v;
        }

        public static HorizontalLayoutGroup HBox(Transform parent, float spacing, int pad = 0,
                                                 TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = new GameObject("HBox", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.childAlignment = align;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            h.childControlWidth = false;
            h.childControlHeight = false;
            h.padding = new RectOffset(pad, pad, pad, pad);
            return h;
        }
    }
}
