using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core.Data;
using Game.Core.Effects;
using Game.Core.Model;
using Game.Runtime;
using Game.Runtime.Menu;
using Game.Runtime.View;
using UnityEngine;

namespace Game.Qa
{
    /// <summary>
    /// Punto de entrada del sistema de pruebas. Se engancha SOLO al arrancar el juego, desde aquí
    /// hacia el juego y nunca al revés: ninguna clase del juego nombra este ensamblado, así que
    /// borrar la carpeta Qa deja el proyecto compilando y el juego con sus reglas normales.
    /// </summary>
    public static class QaBootstrap
    {
        public static CardCatalog Catalog { get; private set; }
        public static IEffectResolver Resolver { get; private set; }

        private static GameObject _runner;
        private static List<DeckEntry> _decksCache;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            try
            {
                Catalog = UnityCatalogLoader.LoadDefault();
                Resolver = CardEffects.BuildResolver();
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[QA] No se pudo cargar el catálogo: " + e.Message);
                return;
            }

            if (_runner == null)
            {
                _runner = new GameObject("[QA] Pruebas de efectos");
                Object.DontDestroyOnLoad(_runner);
                _runner.AddComponent<QaRunner>();
            }

            // Costuras del juego: las rellena QA, el juego solo las consume si están.
            MatchSnapshot.ExtraCardLine = DescribeCardMarks;
            PlayerData.ExtraDecks = BuildQaDecks;
            PlayerData.ExtraDeckProgress = DeckProgress;

            Debug.Log($"[QA] Pruebas de efectos activas. Resultados en: {QaStore.Path}");
        }

        /// <summary>Al responder hay que tirar las cachés de mazos: si no, se rehacen y el jugador
        /// sigue recibiendo la lista vieja, sin ningún error que lo delate.</summary>
        public static void InvalidateCaches() => _decksCache = null;

        // ---------------- marcas en la ficha de carta ----------------

        /// <summary>Lista TODOS los efectos de la carta, uno por línea. Una carta sin marcas se
        /// leería igual que una carta sin efectos, así que también se pinta lo no probado.</summary>
        private static string DescribeCardMarks(CardDefinition d)
        {
            if (Resolver == null || d == null) return "";
            var sb = new StringBuilder();
            foreach (var ef in QaEffects.Of(d, Resolver))
            {
                var e = QaStore.Find(ef.Key);
                string marca = e == null ? "· sin probar"
                    : e.AsVerdict() switch
                    {
                        QaVerdict.Ok => "✔ correcto",
                        QaVerdict.Problem => "✘ falla: " + e.Problem,
                        _ => "… pendiente",
                    };
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("[QA] ").Append(ef.Label).Append(": ").Append(marca);
            }
            return sb.ToString();
        }

        // ---------------- mazos de prueba ----------------

        /// <summary>Un mazo por historia y variante, ordenados por lo que FALTA por comprobar, y
        /// retirando los que ya están completos. Así el jugador recibe siempre cartas sin probar.</summary>
        private static List<DeckEntry> BuildQaDecks()
        {
            if (_decksCache != null) return _decksCache;
            var lista = new List<DeckEntry>();
            if (Catalog == null || Resolver == null) return _decksCache = lista;

            var pendientes = new List<(DeckEntry deck, int falta)>();

            foreach (var historia in Catalog.Historias)
                for (int v = 0; v < StoryDecks.Variants; v++)
                {
                    if (StoryDecks.For(historia.Id, v) == null) continue;
                    string deckId = $"qa:{historia.Id}:{v}";
                    if (QaStore.Data.RetiredDecks.Contains(deckId)) continue;

                    var built = SampleDeckBuilder.Build(Catalog, historia.Id, 40, 3, v);
                    var defs = built.MainCardIds.Distinct()
                                    .Where(id => Catalog.TryGet(id, out _))
                                    .Select(id => Catalog.Get(id))
                                    .ToList();

                    var (hechos, total, pend) = QaEffects.Progress(defs, Resolver);
                    int falta = total - hechos;

                    // Mazo con todo comprobado: se retira solo de la lista.
                    if (total > 0 && falta == 0) { QaStore.RetireDeck(deckId); continue; }

                    pendientes.Add((new DeckEntry
                    {
                        nombre = $"[QA] {historia.Nombre} · {(char)('A' + v)}",
                        historiaId = historia.Id,
                        cartas = new List<string>(built.MainCardIds),
                        insignia = built.MainCardIds.Count > 0 ? built.MainCardIds[0] : "",
                    }, falta));
                }

            // Delante lo que más falta por comprobar. Lista estable: se ordena y se concatena,
            // sin Sort en sitio (que no es estable y haría bailar el orden entre partidas).
            lista.AddRange(pendientes.OrderByDescending(x => x.falta).Select(x => x.deck));
            return _decksCache = lista;
        }

        /// <summary>Progreso contando EFECTOS, no cartas: por cartas mentiría en cuanto una carta
        /// tiene dos efectos.</summary>
        private static string DeckProgress(DeckEntry deck)
        {
            if (Catalog == null || Resolver == null || deck?.cartas == null) return "";
            var defs = deck.cartas.Distinct()
                           .Where(id => Catalog.TryGet(id, out _))
                           .Select(id => Catalog.Get(id));
            var (hechos, total, pend) = QaEffects.Progress(defs, Resolver);
            return pend > 0 ? $"{hechos}/{total} efectos · {pend} pendientes" : $"{hechos}/{total} efectos";
        }
    }
}
