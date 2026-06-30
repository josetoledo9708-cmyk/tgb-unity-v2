using UnityEditor;

namespace Game.Editor
{
    /// <summary>
    /// Importa los PNG bajo Assets/Game/Art/Cards/ como Sprites (Sprite (2D and UI)), en vez del
    /// tipo Default. Así quedan como assets Sprite reutilizables (UI, prefabs, etc.).
    /// </summary>
    public sealed class CardSpriteImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Game/Art/Cards/")) return;

            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed; // cartas: sin artefactos
        }
    }
}
