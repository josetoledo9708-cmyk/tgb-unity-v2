using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Menu
{
    /// <summary>Carga y cachea sprites/fuente de los menús desde Resources/Menu/.</summary>
    public static class MenuAssets
    {
        private static readonly Dictionary<string, Sprite> _sprites = new();
        private static Font _font;

        public static Sprite Sprite(string path)
        {
            if (_sprites.TryGetValue(path, out var s)) return s;
            s = Resources.Load<Sprite>("Menu/" + path);
            _sprites[path] = s;
            return s;
        }

        public static Font Font()
        {
            if (_font != null) return _font;
            _font = Resources.Load<Font>("Menu/fonts/Cinzel-Variable")
                    ?? Resources.Load<Font>("Menu/fonts/CinzelDecorative-Regular")
                    ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }
    }
}
