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

        private static void Configure()
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.rinco.thegreatbook");
            PlayerSettings.productName = "The Great Book";
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24; // Android 7+
            PlayerSettings.Android.forceInternetPermission = true; // LAN/Netcode requiere permiso de red
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft; // el juego es horizontal
            // IL2CPP + ARM64 (requerido por Play; para pruebas locales también vale)
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        }
    }
}
