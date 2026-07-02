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
            var name = Path.GetFileNameWithoutExtension(assetPath);
            if (name == "Botones")
                ti.spriteBorder = new Vector4(90f, 90f, 90f, 90f);
            // Marco de libro (Constructor de mazos): borde ornamentado, 9-slice sin deformarlo.
            else if (name == "Mazo")
                ti.spriteBorder = new Vector4(30f, 30f, 30f, 30f);
            else if (name == "BotonCrearNueva")
                ti.spriteBorder = new Vector4(24f, 24f, 24f, 24f);
        }
    }
}
