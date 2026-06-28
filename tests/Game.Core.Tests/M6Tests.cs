using Game.Core.Effects;
using Game.Core.Engine;
using Game.Core.Model;
using Game.Core.View;

namespace Game.Core.Tests
{
    public static class M6Tests
    {
        private static GameEngine NewGameFx(CardCatalog cat, ulong seed, string h0 = "h1")
        {
            var eng = M1Tests.NewGame(cat, seed, h0: h0);
            eng.Effects = CardEffects.BuildResolver();
            return eng;
        }

        public static void Run(TestRunner t, CardCatalog cat)
        {
            t.Case("M6 e2e: armar las 5 piezas de h1 y ganar en Entrega (Victoria III)", () =>
            {
                var eng = NewGameFx(cat, 500, h0: "h1");
                var p = eng.State.Players[0];
                p.Fd = 12;
                var jardin = eng.NewInstance(cat.Get("t01"), 0);
                var arbol = eng.NewInstance(cat.Get("t02"), 0);
                var adan = eng.NewInstance(cat.Get("sh01"), 0);
                var eva = eng.NewInstance(cat.Get("sh02"), 0);
                var serp = eng.NewInstance(cat.Get("sa1"), 0);
                foreach (var c in new[] { jardin, arbol, adan, eva, serp }) p.Mano.Add(c);

                TestRunner.IsTrue(eng.PlayTierra(jardin).Ok, "Jardín");
                TestRunner.IsTrue(eng.PlayTierraFree(arbol).Ok, "Árbol (free)");
                TestRunner.IsTrue(eng.PlaySer(adan).Ok, "Adán");
                TestRunner.IsTrue(eng.PlaySer(eva).Ok, "Eva");
                TestRunner.IsTrue(eng.PlaySer(serp).Ok, "Serpiente");

                var res = eng.EndTurn();
                TestRunner.IsTrue(eng.State.IsOver, "debió ganar: " + res);
                TestRunner.AreEqual(0, eng.State.Winner!.Value);
                TestRunner.AreEqual(VictoryId.III, eng.State.WinReason!.Value);
            });

            t.Case("M6 smoke: 6 turnos alternados sin errores; FD resetea en Preludio", () =>
            {
                var eng = NewGameFx(cat, 501);
                for (int i = 0; i < 6; i++)
                {
                    var r = eng.EndTurn();
                    TestRunner.IsTrue(r.Ok, $"turno {i}: " + r.Error);
                    if (eng.State.IsOver) break;
                }
                TestRunner.IsTrue(!eng.State.IsOver, "no debería terminar en 6 turnos vacíos");
                TestRunner.AreEqual(0, eng.State.Active.Fd, "FD del activo reseteado en su Preludio");
                TestRunner.AreEqual(7, eng.State.TurnNumber);
            });

            t.Case("M6 ConsoleView renderiza el estado sin romper", () =>
            {
                var eng = NewGameFx(cat, 502);
                string txt = ConsoleView.Render(eng.State);
                TestRunner.IsTrue(txt.Contains("Turno"), "incluye encabezado");
                TestRunner.IsTrue(txt.Contains("P0") && txt.Contains("P1"), "ambos jugadores");
                TestRunner.IsTrue(txt.Contains("Tierras"), "muestra zonas");
            });

            t.Case("M6 e2e determinista: misma semilla -> mismo log", () =>
            {
                var a = NewGameFx(cat, 777);
                var b = NewGameFx(cat, 777);
                a.EndTurn(); a.EndTurn();
                b.EndTurn(); b.EndTurn();
                TestRunner.AreEqual(string.Join("\n", a.State.Log),
                                    string.Join("\n", b.State.Log), "logs idénticos");
            });
        }
    }
}
