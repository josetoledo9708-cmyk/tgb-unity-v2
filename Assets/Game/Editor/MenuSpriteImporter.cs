using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Importa los PNG bajo Resources/Menu/ como Sprites (uGUI). Comprime las ilustraciones
    /// grandes y añade overrides ASTC para móvil (Android/iOS) para reducir memoria; deja los marcos
    /// pequeños sin comprimir para conservar la nitidez del oro.</summary>
    public sealed class MenuSpriteImporter : AssetPostprocessor
    {
        private void OnPreprocessTexture()
        {
            var p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Resources/Menu/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = false;
            ti.wrapMode = TextureWrapMode.Clamp;

            var name = Path.GetFileNameWithoutExtension(assetPath);

            // Ilustraciones grandes (fotos/cartas/fondos): comprimidas y a menor resolución.
            bool bigArt = p.Contains("/cartas_historia/") || p.Contains("/historias/bg_")
                       || p.Contains("/tienda/") || p.Contains("/tomos/") || p.Contains("/creador/")
                       || p.Contains("/mazos/") || name == "main_menu_bg"
                       || name.StartsWith("fondo") || name.StartsWith("Fondo");
            if (bigArt)
            {
                ti.textureCompression = TextureImporterCompression.Compressed;
                ti.maxTextureSize = (p.Contains("/cartas_historia/") || p.Contains("/historias/bg_")) ? 512 : 1024;

                // móvil: ASTC (mucho menos memoria en teléfono)
                foreach (var plat in new[] { "Android", "iPhone" })
                {
                    var ps = ti.GetPlatformTextureSettings(plat);
                    ps.overridden = true;
                    ps.maxTextureSize = ti.maxTextureSize;
                    ps.format = TextureImporterFormat.ASTC_6x6;
                    ps.textureCompression = TextureImporterCompression.Compressed;
                    ti.SetPlatformTextureSettings(ps);
                }
            }
            else
            {
                // UI pequeña (marcos, botones, iconos): sin comprimir (evita artefactos en el oro),
                // pero con tope de tamaño para no cargar iconos gigantes (p.ej. Moneda).
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                ti.maxTextureSize = name == "Moneda" ? 256 : 512;
            }

            // --- spriteBorder para 9-slice ---
            // Botones: el marco es ancho y bajo; el border debe ser menor a la mitad de la altura del
            // botón (~92px) para que el 9-slice deje un centro plano (recuadro), no una cápsula/óvalo.
            if (name == "Botones")
            {
                ti.maxTextureSize = 1024;                        // más nitidez para el marco
                ti.spriteBorder = new Vector4(120f, 40f, 120f, 40f); // x,z (puntas) grandes; y,w (arriba/abajo) chicos
            }
            else if (name is "BtnGold" or "BtnRojo" or "BtnVerde")
                ti.spriteBorder = new Vector4(20f, 20f, 20f, 20f);
            else if (name == "Mazo")
                ti.spriteBorder = new Vector4(30f, 30f, 30f, 30f);
            else if (name == "BotonCrearNueva")
                ti.spriteBorder = new Vector4(24f, 24f, 24f, 24f);
            else if (name is "borrar" or "cerrar" or "renombrar" or "cancelar" or "btn_aceptar"
                          or "cambiar_portada" or "editar_cartas")
                ti.spriteBorder = new Vector4(26f, 26f, 26f, 26f);
            else if (name is "fondo" or "renombrar_marco" or "fondo_portada")
                ti.spriteBorder = new Vector4(90f, 90f, 90f, 90f);
        }
    }
}
