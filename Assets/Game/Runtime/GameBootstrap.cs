using System.IO;
using UnityEngine;
using Game.Core.Data;
using Game.Core.Effects;
using Game.Core.Engine;
using Game.Core.View;

namespace Game.Runtime
{
    /// <summary>
    /// Arranque mínimo de la demo: carga el catálogo desde StreamingAssets, monta una partida
    /// con dos mazos de muestra, juega un par de turnos automáticos y vuelca el estado al log.
    /// Sirve para verificar que el Core corre dentro de Unity. La presentación 3D real (tablero,
    /// cámara, arrastrar cartas) se construye encima de este motor.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private string historiaP0 = "h1";
        [SerializeField] private string historiaP1 = "h2";
        [SerializeField] private ulong seed = 12345;

        public GameEngine Engine { get; private set; }

        private void Start()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "catalogo.v3.json");
            if (!File.Exists(path))
            {
                Debug.LogError($"No se encontró el catálogo en {path}");
                return;
            }

            var catalog = UnityCatalogLoader.FromJson(File.ReadAllText(path));
            Engine = new GameEngine(catalog) { Effects = CardEffects.BuildResolver() };
            Engine.StartGame(
                SampleDeckBuilder.Build(catalog, historiaP0, 40),
                SampleDeckBuilder.Build(catalog, historiaP1, 40),
                seed, firstPlayer: 0);

            Debug.Log($"The Great Book — catálogo: {catalog.Cards.Count} cartas. Partida iniciada.");
            Engine.EndTurn();
            Engine.EndTurn();
            Debug.Log(ConsoleView.Render(Engine.State));
        }
    }
}
