using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Game.Core.Data;
using Game.Core.Effects;
using Game.Core.Engine;
using Game.Core.Model;
using Game.Runtime;

namespace Game.Tests
{
    /// <summary>
    /// v0.01 — Los mazos por historia deben ser legales y, en conjunto, tocar TODO el catálogo:
    /// esa es la condición para poder probar cada carta y sus interacciones jugando una partida
    /// por historia.
    /// </summary>
    public class StoryDeckTests
    {
        private static CardCatalog LoadCatalog()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "catalogo.v3.json");
            return UnityCatalogLoader.FromJson(File.ReadAllText(path));
        }

        [Test]
        public void Story_Decks_Cover_Whole_Catalog()
        {
            var cat = LoadCatalog();
            var faltan = StoryDecks.NotCovered(cat);
            Debug.Log($"Cartas del catálogo sin cubrir por los mazos de historia: {faltan.Count}");
            Assert.IsEmpty(faltan,
                "Estas cartas no aparecen en ningún mazo por historia, así que no se prueban:\n"
                + string.Join(", ", faltan));
        }

        [Test]
        public void Every_Story_Deck_Is_Legal_And_Startable()
        {
            var cat = LoadCatalog();
            foreach (var historia in cat.Historias)
            for (int variant = 0; variant < StoryDecks.Variants; variant++)
            {
                string etiqueta = $"{historia.Id}-{(char)('A' + variant)}";
                var deck = SampleDeckBuilder.Build(cat, historia.Id, 40, pieceCopies: 3, variant: variant);
                Assert.AreEqual(40, deck.MainCardIds.Count, $"{etiqueta}: tamaño de mazo incorrecto");

                // Ninguna carta puede exceder el máximo de copias permitido.
                var exceso = deck.MainCardIds.GroupBy(x => x)
                                 .Where(g => g.Count() > DeckValidator.MaxCopies)
                                 .Select(g => $"{g.Key} x{g.Count()}")
                                 .ToList();
                Assert.IsEmpty(exceso, $"{etiqueta}: demasiadas copias -> {string.Join(", ", exceso)}");

                // Y debe poder arrancar una partida real: variante A contra variante B.
                var eng = new GameEngine(cat)
                {
                    Effects = CardEffects.BuildResolver(),
                    Decisions = new AutoDecisionProvider(cat),
                };
                Assert.DoesNotThrow(
                    () => eng.StartGame(deck, SampleDeckBuilder.Build(cat, historia.Id, 40, 3, 1 - variant), 7, 0),
                    $"{etiqueta}: la partida no arranca con su mazo");
            }
        }

        /// <summary>Dos partidas con semillas distintas deben repartir manos distintas: si no, el
        /// mazo no se está barajando de verdad (bug de la semilla fija).</summary>
        [Test]
        public void Different_Seeds_Give_Different_Opening_Hands()
        {
            var cat = LoadCatalog();

            string Hand(ulong seed)
            {
                var eng = new GameEngine(cat)
                {
                    Effects = CardEffects.BuildResolver(),
                    Decisions = new AutoDecisionProvider(cat),
                };
                eng.StartGame(SampleDeckBuilder.Build(cat, "h1", 40),
                              SampleDeckBuilder.Build(cat, "h2", 40), seed, 0);
                return string.Join(",", eng.State.Players[0].Mano.Cards.Select(c => c.Def.Id));
            }

            Assert.AreNotEqual(Hand(1), Hand(2), "Distinta semilla debería dar distinta mano inicial.");
        }
    }
}
