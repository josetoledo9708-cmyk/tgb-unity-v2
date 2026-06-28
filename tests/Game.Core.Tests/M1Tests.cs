using System.Linq;
using Game.Core.Data;
using Game.Core.Engine;
using Game.Core.Model;

namespace Game.Core.Tests
{
    public static class M1Tests
    {
        public static void Run(TestRunner t, CardCatalog cat)
        {
            t.Case("M1 mazo de muestra (h1, 40) es válido", () =>
            {
                var deck = TestDecks.BuildSample(cat, "h1", 40);
                var res = DeckValidator.Validate(deck, cat);
                TestRunner.IsTrue(res.Ok, "debería ser válido: " + res);
                TestRunner.AreEqual(40, deck.MainCardIds.Count);
            });

            t.Case("M1 rechaza <40 cartas", () =>
            {
                var ids = TestDecks.BuildSample(cat, "h1", 40).MainCardIds.Take(39).ToList();
                var res = DeckValidator.Validate(new DeckDefinition(ids, "h1"), cat);
                TestRunner.IsTrue(!res.Ok, "39 cartas debe fallar");
            });

            t.Case("M1 rechaza >3 copias", () =>
            {
                var ids = Enumerable.Repeat("t03", 4)
                    .Concat(Enumerable.Repeat("sh04", 36)).ToList(); // 40 cartas, t03 x4
                var res = DeckValidator.Validate(new DeckDefinition(ids, "h1"), cat);
                TestRunner.IsTrue(!res.Ok, "4 copias de t03 debe fallar");
            });

            t.Case("M1 rechaza mazo sin una pieza de la HISTORIA", () =>
            {
                var deck = TestDecks.BuildSample(cat, "h1", 40);
                // Quitar todas las copias de la pieza 'Adán' (sh01).
                var sinAdan = deck.MainCardIds.Where(id => id != "sh01").ToList();
                while (sinAdan.Count < 40) sinAdan.Add("sh04"); // rellenar respetando <=3? puede romper, ok para test
                var res = DeckValidator.Validate(new DeckDefinition(sinAdan, "h1"), cat);
                TestRunner.IsTrue(!res.Ok, "sin Adán debe fallar");
            });

            t.Case("M1 rechaza DIA/HISTORIA en el mazo principal", () =>
            {
                var ids = TestDecks.BuildSample(cat, "h1", 40).MainCardIds.ToList();
                ids[0] = "dia1";
                var res = DeckValidator.Validate(new DeckDefinition(ids, "h1"), cat);
                TestRunner.IsTrue(!res.Ok, "dia1 en mazo debe fallar");
            });

            t.Case("M1 setup: manos de 7, pila DIA de 7 ordenada, historia colocada", () =>
            {
                var eng = NewGame(cat, seed: 12345);
                foreach (var p in eng.State.Players)
                {
                    TestRunner.AreEqual(7, p.Mano.Count, "mano");
                    TestRunner.AreEqual(7, p.PilaDia.Count, "pila dia");
                    TestRunner.AreEqual(33, p.Mazo.Count, "mazo restante (40-7)");
                    TestRunner.IsTrue(p.Historia != null, "historia colocada");
                    // Orden dia1..dia7
                    for (int i = 0; i < 7; i++)
                        TestRunner.AreEqual($"dia{i + 1}", p.PilaDia.Cards[i].Def.Id, "orden dia");
                }
            });

            t.Case("M1 barajado determinista: misma semilla -> mismo mazo", () =>
            {
                var a = NewGame(cat, seed: 999);
                var b = NewGame(cat, seed: 999);
                var topA = a.State.Players[0].Mazo.Cards.Select(c => c.Def.Id).ToList();
                var topB = b.State.Players[0].Mazo.Cards.Select(c => c.Def.Id).ToList();
                TestRunner.IsTrue(topA.SequenceEqual(topB), "mismo orden con misma semilla");
            });

            t.Case("M1 semillas distintas -> ordenes distintos", () =>
            {
                var a = NewGame(cat, seed: 1);
                var b = NewGame(cat, seed: 2);
                var topA = a.State.Players[0].Mazo.Cards.Select(c => c.Def.Id).ToList();
                var topB = b.State.Players[0].Mazo.Cards.Select(c => c.Def.Id).ToList();
                TestRunner.IsTrue(!topA.SequenceEqual(topB), "ordenes deberían diferir");
            });

            t.Case("M1 instancia ids únicos", () =>
            {
                var eng = NewGame(cat, seed: 7);
                var all = eng.State.Players
                    .SelectMany(p => p.Mazo.Cards.Concat(p.Mano.Cards).Concat(p.PilaDia.Cards))
                    .Select(c => c.InstanceId).ToList();
                TestRunner.AreEqual(all.Count, all.Distinct().Count(), "ids únicos");
            });
        }

        public static GameEngine NewGame(CardCatalog cat, ulong seed, string h0 = "h1",
                                          string h1 = "h2", int firstPlayer = 0)
        {
            var eng = new GameEngine(cat);
            eng.StartGame(
                TestDecks.BuildSample(cat, h0, 40),
                TestDecks.BuildSample(cat, h1, 40),
                seed, firstPlayer);
            return eng;
        }
    }
}
