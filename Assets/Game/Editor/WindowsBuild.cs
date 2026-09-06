using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// Build de Windows (EXE) para probar el Multijugador LAN entre dos PCs. Menú: "The Great Book".
    /// </summary>
    public static class WindowsBuild
    {
        private const string OutDir = "Build/Windows";
        private const string Exe = "TheGreatBook.exe";

        [MenuItem("The Great Book/Windows/Build EXE")]
        public static void BuildExe()
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

            Directory.CreateDirectory(OutDir);
            string path = Path.Combine(OutDir, Exe);

            var opts = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = path,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None,
            };

            var report = BuildPipeline.BuildPlayer(opts);
            var s = report.summary;
            if (s.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                Debug.Log($"EXE generado: {Path.GetFullPath(OutDir)} ({s.totalSize / (1024 * 1024)} MB). Copia toda la carpeta '{OutDir}' a la otra PC y ejecuta {Exe}.");
            else
                Debug.LogError($"Build Windows falló: {s.result} ({s.totalErrors} errores).");
        }
    }
}
