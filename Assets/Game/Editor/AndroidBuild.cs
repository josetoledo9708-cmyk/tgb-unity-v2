using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Build de Android (APK) para probar el Multijugador LAN en teléfono. Menú: "The Great Book".
    /// Requiere el módulo "Android Build Support" (SDK/NDK/JDK) instalado en Unity Hub.
    /// </summary>
    public static class AndroidBuild
    {
        private const string OutDir = "Build/Android";
        private const string Apk = "TheGreatBook.apk";

        [MenuItem("The Great Book/Android/Configurar plataforma Android")]
        public static void SwitchToAndroid()
        {
            Configure();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            Debug.Log("Plataforma cambiada a Android y ajustes aplicados.");
        }

        [MenuItem("The Great Book/Android/Build APK")]
        public static void BuildApk()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
                if (string.IsNullOrEmpty(active))
                {
                    Debug.LogError("No hay escenas en Build Settings ni escena guardada. Guarda la escena y añádela (File > Build Settings).");
                    return;
                }
                scenes = new[] { active };
            }

            Configure();
            Directory.CreateDirectory(OutDir);
            string path = Path.Combine(OutDir, Apk);

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            if (s.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                Debug.Log($"APK generado: {Path.GetFullPath(path)} ({s.totalSize / (1024 * 1024)} MB)");
            else
                Debug.LogError($"Build Android falló: {s.result} ({s.totalErrors} errores).");
        }

        // Arte de MARCOS/BOTONES (líneas doradas finas): se degradan al comprimir en Android.
        private static readonly string[] UiFrameKeywords =
        { "boton", "marco", "glow", "recuadro", "cuadro", "banner", "sel_", "btn", "diseno" };

        [MenuItem("The Great Book/Android/Transcodificar video del menú")]
        public static void TranscodeMenuVideo()
        {
            const string path = "Assets/Game/Resources/Menu/tomos/fondo_tomos.mp4";
            if (AssetImporter.GetAtPath(path) is not VideoClipImporter vi)
            {
                Debug.LogError("No se encontró el VideoClip: " + path);
                return;
            }
            var s = vi.defaultTargetSettings;
            s.enableTranscoding = true;      // re-encoda a formato compatible (Android no reproduce el mp4 original)
            s.codec = VideoCodec.H264;
            vi.defaultTargetSettings = s;
            vi.SetTargetSettings(BuildTargetGroup.Android, s);
            vi.SaveAndReimport();
            Debug.Log("Video del menú transcodificado (H264).");
        }

        [MenuItem("The Great Book/Reimportar marcos UI")]
        public static void ReimportUiFrames()
        {
            foreach (var n in new[] { "Botones", "BtnGold", "BtnRojo", "BtnVerde" })
            {
                var path = $"Assets/Game/Resources/Menu/{n}.png";
                if (System.IO.File.Exists(path)) AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }
            AssetDatabase.Refresh();
            Debug.Log("Marcos UI reimportados (border actualizado).");
        }

        [MenuItem("The Great Book/Android/Arreglar texturas UI (sin comprimir)")]
        public static void FixUiTextures()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Game/Resources/Menu" });
            int ui = 0, reset = 0;
            foreach (var g in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(g);
                if (AssetImporter.GetAtPath(path) is not TextureImporter ti) continue;

                string file = System.IO.Path.GetFileName(path).ToLowerInvariant();
                bool isUi = UiFrameKeywords.Any(k => file.Contains(k));

                var s = ti.GetPlatformTextureSettings("Android");
                if (isUi)
                {
                    s.overridden = true;
                    s.format = TextureImporterFormat.RGBA32;   // sin comprimir: líneas nítidas
                    s.textureCompression = TextureImporterCompression.Uncompressed;
                    s.maxTextureSize = Mathf.Max(s.maxTextureSize, 2048);
                    ui++;
                }
                else
                {
                    if (!s.overridden) continue; // ya está en compresión por defecto
                    s.overridden = false;        // revertir: usar compresión Android por defecto (arte de cartas/animaciones)
                    reset++;
                }
                ti.SetPlatformTextureSettings(s);
                ti.SaveAndReimport();
            }
            Debug.Log($"Android: {ui} texturas de UI a RGBA32; {reset} revertidas a compresión por defecto.");
        }

        private static void Configure()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.rinco.thegreatbook");
            PlayerSettings.productName = "The Great Book";
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24; // Android 7+
            PlayerSettings.Android.forceInternetPermission = true; // LAN/Netcode requiere permiso de red
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft; // el juego es horizontal

            // IL2CPP + ARM64 (requerido por Play; una sola arquitectura = APK más chico)
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // --- Optimización de tamaño de descarga ---
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.High); // recorta código no usado
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Master); // binario más optimizado/chico
            PlayerSettings.stripEngineCode = true;                 // quita módulos del motor no usados
            PlayerSettings.Android.optimizedFramePacing = true;    // frame pacing suave en móvil
            PlayerSettings.Android.splitApplicationBinary = false; // un solo APK (no partir el binario)
            EditorUserBuildSettings.buildAppBundle = false;        // APK (no AAB) para instalar directo
            PlayerSettings.gcIncremental = true;                   // GC incremental (menos hitches)
        }
    }
}
