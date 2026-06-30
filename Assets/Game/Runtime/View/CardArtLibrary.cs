using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Game.Runtime.View
{
    /// <summary>
    /// Carga arte de cartas (PNG/JPG) desde una carpeta y lo asocia a cada carta por su nombre,
    /// ignorando acentos/mayúsculas/puntuación. Los archivos son cartas completas ya diseñadas
    /// (marco + ilustración + nombre + coste). Carga perezosa con caché.
    ///
    /// Convención de archivos: "Nombre de la carta _ subtítulo.png". Se empareja por el prefijo
    /// (el nombre), eligiendo el archivo más corto que empiece por el nombre normalizado.
    /// </summary>
    public sealed class CardArtLibrary
    {
        private readonly string _folder;
        private readonly Dictionary<string, string> _files = new();   // normFull -> path
        private readonly Dictionary<string, Texture2D?> _cache = new(); // cardKey -> tex

        // Alias para nombres que no coinciden con el archivo.
        private static readonly Dictionary<string, string> Alias = new()
        {
            { "laluz", "luzytinieblas" },                 // dia1
            { "losseresdelagua", "criaturasdemarycielos" }, // dia5
            { "criaturasmarinas", "criaturasdemarycielos" } // sa5
        };

        private Texture2D? _back;
        private bool _backTried;

        public CardArtLibrary(string folder)
        {
            _folder = folder;
            Index();
        }

        public bool Available => _files.Count > 0;

        private void Index()
        {
            if (!Directory.Exists(_folder)) return;
            foreach (var pattern in new[] { "*.png", "*.jpg", "*.jpeg" })
                foreach (var f in Directory.GetFiles(_folder, pattern))
                {
                    var key = Norm(Path.GetFileNameWithoutExtension(f));
                    if (!_files.ContainsKey(key)) _files[key] = f;
                }
        }

        public Texture2D? Front(string cardName)
        {
            var key = Norm(cardName);
            if (Alias.TryGetValue(key, out var a)) key = a;
            if (_cache.TryGetValue(key, out var cached)) return cached;

            // Empareja por prefijo común más largo (tolera nombres más largos/cortos que el archivo).
            string? best = null;
            int bestScore = 0;
            int bestLen = int.MaxValue;
            int threshold = Mathf.Min(key.Length, 6);
            foreach (var kv in _files)
            {
                int cp = CommonPrefix(key, kv.Key);
                if (cp < threshold) continue;
                if (cp > bestScore || (cp == bestScore && kv.Key.Length < bestLen))
                {
                    bestScore = cp;
                    bestLen = kv.Key.Length;
                    best = kv.Value;
                }
            }

            var tex = best != null ? Load(best) : null;
            _cache[key] = tex;
            return tex;
        }

        private static int CommonPrefix(string a, string b)
        {
            int n = Mathf.Min(a.Length, b.Length), i = 0;
            while (i < n && a[i] == b[i]) i++;
            return i;
        }

        public Texture2D? Back()
        {
            if (_backTried) return _back;
            _backTried = true;
            foreach (var kv in _files)
                if (kv.Key.Contains("dorso")) { _back = Load(kv.Value); break; }
            return _back;
        }

        private static Texture2D? Load(string path)
        {
            try
            {
                var bytes = File.ReadAllBytes(path);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, true);
                if (tex.LoadImage(bytes))
                {
                    tex.wrapMode = TextureWrapMode.Clamp;
                    tex.name = Path.GetFileNameWithoutExtension(path);
                    return tex;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"No se pudo cargar arte {path}: {e.Message}");
            }
            return null;
        }

        /// <summary>Minúsculas, sin acentos, solo letras/dígitos.</summary>
        private static string Norm(string s)
        {
            var d = s.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(d.Length);
            foreach (var ch in d)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsLetterOrDigit(ch)) sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }
    }
}
