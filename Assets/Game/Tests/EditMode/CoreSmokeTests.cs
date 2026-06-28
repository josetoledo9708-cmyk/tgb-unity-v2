using System.IO;
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
    /// Tests EditMode que verifican que el Core (movido a Assets/Game/Core) y el loader de
    /// Unity (Newtonsoft) funcionan dentro del editor. La batería completa vive en el proyecto
    /// dev .NET (tests/Game.Core.Tests); aquí va un humo representativo.
    /// </summary>
    public class CoreSmokeTests
    {
        private static CardCatalog LoadCatalog()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "catalogo.v3.json");
            return UnityCatalogLoader.FromJson(File.ReadAllText(path));
        }

        [Test]
        public void Catalog_Loads_With_Expected_Counts()
        {
            var cat = LoadCatalog();
            Assert.AreEqual(7, cat.CountByType(CardType.Dia));
            Assert.AreEqual(20, cat.CountByType(CardType.Tierra));
            Assert.AreEqual(42, cat.CountByType(CardType.Concepto));
            Assert.AreEqual(114, cat.Cards.Count);
            Assert.AreEqual(7, cat.Historias.Count);
        }

        [Test]
        public void Game_Starts_And_Advances_Turns()
        {
            var cat = LoadCatalog();
            var eng = new GameEngine(cat) { Effects = CardEffects.BuildResolver() };
            eng.StartGame(
                SampleDeckBuilder.Build(cat, "h1", 40),
                SampleDeckBuilder.Build(cat, "h2", 40),
                seed: 999, firstPlayer: 0);

            Assert.AreEqual(Phase.Preparacion, eng.State.Phase);
            Assert.IsTrue(eng.EndTurn().Ok);
            Assert.AreEqual(2, eng.State.TurnNumber);
        }

        [Test]
        public void Victory_III_When_Five_Pieces_In_Field()
        {
            var cat = LoadCatalog();
            var eng = new GameEngine(cat) { Effects = CardEffects.BuildResolver() };
            eng.StartGame(SampleDeckBuilder.Build(cat, "h1", 40),
                          SampleDeckBuilder.Build(cat, "h2", 40), 1, 0);
            var p = eng.State.Players[0];
            foreach (var id in new[] { "t01", "t02" }) p.Tierras.Add(eng.NewInstance(cat.Get(id), 0));
            foreach (var id in new[] { "sh01", "sh02", "sa1" }) p.Seres.Add(eng.NewInstance(cat.Get(id), 0));
            eng.EndTurn();
            Assert.IsTrue(eng.State.IsOver);
            Assert.AreEqual(VictoryId.III, eng.State.WinReason);
        }
    }
}
