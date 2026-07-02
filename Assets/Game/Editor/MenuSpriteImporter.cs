using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Importa los PNG bajo Resources/Menu/ como Sprites (para uGUI Image/Button).</summary>
    public sealed class MenuSpriteImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/Menu/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.textureCompression = TextureImporterCompression.Uncompressed;

            // Botones.png: rectángulo redondeado con borde/glow baked. Border generoso para que
            // el 9-slice (Image.Type.Sliced) no deforme las esquinas ni el brillo al estirar.
            if (Path.GetFileNameWithoutExtension(assetPath) == "Botones")
                ti.spriteBorder = new Vector4(90f, 90f, 90f, 90f);
        }
    }
}
