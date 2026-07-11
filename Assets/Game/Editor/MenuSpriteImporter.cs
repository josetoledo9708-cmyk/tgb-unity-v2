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
            // Botones universales (Blender): marco metálico + centro liso, esquinas redondeadas
            // (~19px en el render 480x185). Border 20 (>= radio) para 9-slice sin deformar el marco:
            // 20+20=40 encaja hasta en los botones pequeños (engranaje/tutorial ~40px) y deja centro
            // estirable en los normales (48-70px).
            else if (name is "BtnGold" or "BtnRojo" or "BtnVerde")
                ti.spriteBorder = new Vector4(20f, 20f, 20f, 20f);
            // Marco de libro (Constructor de mazos): borde ornamentado, 9-slice sin deformarlo.
            else if (name == "Mazo")
                ti.spriteBorder = new Vector4(30f, 30f, 30f, 30f);
            else if (name == "BotonCrearNueva")
                ti.spriteBorder = new Vector4(24f, 24f, 24f, 24f);
            // Modal "opciones de mazo": botones pequeños + fondos grandes, todos con esquinas
            // redondeadas transparentes. Border generoso para 9-slice sin deformar.
            else if (name is "borrar" or "cerrar" or "renombrar" or "cancelar" or "btn_aceptar"
                          or "cambiar_portada" or "editar_cartas")
                ti.spriteBorder = new Vector4(26f, 26f, 26f, 26f);
            else if (name is "fondo" or "renombrar_marco" or "fondo_portada")
                ti.spriteBorder = new Vector4(90f, 90f, 90f, 90f);
        }
    }
}
