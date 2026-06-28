using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Game.Runtime.View;

namespace Game.Editor
{
    /// <summary>
    /// Crea (o recrea) la escena jugable Main con un GameObject que tiene HotseatView.
    /// Ejecutable desde el menú o por línea de comandos (-executeMethod).
    /// </summary>
    public static class SceneBootstrap
    {
        private const string ScenePath = "Assets/Scenes/Main.unity";

        [MenuItem("The Great Book/Crear escena Main")]
        public static void CreateMainScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var controller = new GameObject("GameController");
            controller.AddComponent<HotseatView>();

            Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, ScenePath);

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(ScenePath, true)
            };

            Debug.Log($"Escena creada: {ScenePath} (con HotseatView). Pulsa Play.");
        }
    }
}
