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
            ti.isReadable = true; // permite samplear pixeles en código (p.ej. MetallicGoldGradient)
        }
    }
}
