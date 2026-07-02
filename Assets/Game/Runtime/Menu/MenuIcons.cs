using UnityEngine;
using UnityEngine.UI;

namespace Game.Runtime.Menu
{
    /// <summary>Iconos de los 4 botones principales, construidos con formas primitivas (sin PNG):
    /// libro (Historias), personas (Multijugador), cartas (Constructor), medalla (Misiones).</summary>
    public static class MenuIcons
    {
        private const float Size = 26f;

        private static RectTransform Root(Transform parent)
        {
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(Size, Size);
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(14f + Size * 0.5f, 0f);
            return rt;
        }

        private static void Shape(Transform parent, Sprite sprite, Color color, Vector2 size, Vector2 pos, float rot = 0f)
        {
            var go = new GameObject("Shape", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
            rt.localRotation = Quaternion.Euler(0f, 0f, rot);
        }

        public static RectTransform Book(Transform parent)
        {
            var root = Root(parent);
            Shape(root, MenuGraphics.Rounded(32, 5), MenuTheme.MetalGold, new Vector2(18f, 22f), Vector2.zero);
            Shape(root, MenuGraphics.Rounded(32, 1), new Color(0f, 0f, 0f, 0.65f), new Vector2(2f, 18f), Vector2.zero); // lomo
            return root;
        }

        public static RectTransform People(Transform parent)
        {
            var root = Root(parent);
            Shape(root, MenuGraphics.Rounded(32, 16), MenuTheme.MetalGold, new Vector2(13f, 13f), new Vector2(-4f, 1f));
            Shape(root, MenuGraphics.Rounded(32, 16), MenuTheme.MetalGold, new Vector2(13f, 13f), new Vector2(4f, -1f));
            return root;
        }

        public static RectTransform Cards(Transform parent)
        {
            var root = Root(parent);
            Shape(root, MenuGraphics.Rounded(32, 4), new Color(0.55f, 0.37f, 0.17f, 1f), new Vector2(14f, 20f), new Vector2(-3f, -2f), -14f);
            Shape(root, MenuGraphics.Rounded(32, 4), MenuTheme.MetalGold, new Vector2(14f, 20f), new Vector2(3f, -2f), 14f);
            return root;
        }

        public static RectTransform Medal(Transform parent)
        {
            var root = Root(parent);
            Shape(root, MenuGraphics.Rounded(32, 16), MenuTheme.MetalGold, new Vector2(20f, 20f), new Vector2(0f, 2f));
            Shape(root, MenuGraphics.Rounded(32, 8), Color.black, new Vector2(10f, 10f), new Vector2(0f, 2f));
            Shape(root, MenuGraphics.Rounded(16, 3), MenuTheme.MetalGold, new Vector2(8f, 8f), new Vector2(0f, 2f), 45f);
            return root;
        }
    }
}
