using System.Collections.Generic;
using Game.Core.Engine;
using Game.Core.Model;

namespace Game.Core.Tests
{
    public static class M2Tests
    {
        public static void Run(TestRunner t, CardCatalog cat)
        {
            t.Case("M2 arranca en Preparación; primer jugador no robó (mano 7)", () =>
            {
                var eng = M1Tests.NewGame(cat, 100, firstPlayer: 0);
                TestRunner.AreEqual(Phase.Preparacion, eng.State.Phase);
                TestRunner.AreEqual(0, eng.State.ActivePlayer);
                TestRunner.AreEqual(7, eng.State.Players[0].Mano.Count, "primer jugador sigue en 7");
            });

            t.Case("M2 PlayTierra coloca y bloquea 2ª TIERRA del turno", () =>
            {
                var eng = M1Tests.NewGame(cat, 101);
                var p = eng.State.Active;
                var t1 = eng.NewInstance(cat.Get("t03"), p.Id); p.Mano.Add(t1);
                var t2 = eng.NewInstance(cat.Get("t06"), p.Id); p.Mano.Add(t2);
                TestRunner.IsTrue(eng.PlayTierra(t1).Ok, "1ª tierra ok");
                TestRunner.AreEqual(1, p.Tierras.Count);
                TestRunner.IsTrue(!eng.PlayTierra(t2).Ok, "2ª tierra debe fallar");
            });

            t.Case("M2 TapTierra suma FD según fd de la carta", () =>
            {
                var eng = M1Tests.NewGame(cat, 102);
                var p = eng.State.Active;
                var sodoma = eng.NewInstance(cat.Get("t08"), p.Id); // fd=2
                p.Tierras.Add(sodoma);
                TestRunner.IsTrue(eng.TapTierra(sodoma).Ok);
                TestRunner.AreEqual(2, p.Fd, "Sodoma da 2 FD");
                TestRunner.IsTrue(!eng.TapTierra(sodoma).Ok, "no re-tapear");
            });

            t.Case("M2 límite de 7 TIERRAs en campo", () =>
            {
                var eng = M1Tests.NewGame(cat, 103);
                var p = eng.State.Active;
                for (int i = 0; i < 7; i++) p.Tierras.Add(eng.NewInstance(cat.Get("t03"), p.Id));
                var extra = eng.NewInstance(cat.Get("t03"), p.Id); p.Mano.Add(extra);
                TestRunner.IsTrue(!eng.PlayTierra(extra).Ok, "8ª tierra debe fallar");
            });

            t.Case("M2 PlaySer paga FD y respeta máx 3", () =>
            {
                var eng = M1Tests.NewGame(cat, 104);
                var p = eng.State.Active; p.Fd = 20;
                foreach (var id in new[] { "sh04", "sh06", "sa2" }) // coste 2,2,1
                {
                    var s = eng.NewInstance(cat.Get(id), p.Id); p.Mano.Add(s);
                    TestRunner.IsTrue(eng.PlaySer(s).Ok, $"play {id}");
                }
                TestRunner.AreEqual(3, p.Seres.Count);
                var cuarto = eng.NewInstance(cat.Get("sa2"), p.Id); p.Mano.Add(cuarto);
                TestRunner.IsTrue(!eng.PlaySer(cuarto).Ok, "4º SER debe fallar");
            });

            t.Case("M2 PlaySer falla sin FD suficiente", () =>
            {
                var eng = M1Tests.NewGame(cat, 105);
                var p = eng.State.Active; p.Fd = 0;
                var noe = eng.NewInstance(cat.Get("sh05"), p.Id); p.Mano.Add(noe); // coste 5
                TestRunner.IsTrue(!eng.PlaySer(noe).Ok, "sin FD debe fallar");
            });

            t.Case("M2 EndTurn pasa el turno y el rival roba (mano 8), turno=2", () =>
            {
                var eng = M1Tests.NewGame(cat, 106, firstPlayer: 0);
                var res = eng.EndTurn();
                TestRunner.IsTrue(res.Ok, "endturn ok: " + res.Error);
                TestRunner.AreEqual(1, eng.State.ActivePlayer, "ahora juega P1");
                TestRunner.AreEqual(2, eng.State.TurnNumber);
                TestRunner.AreEqual(8, eng.State.Players[1].Mano.Count, "P1 robó (7+1)");
            });

            t.Case("M2 Victoria II: rival no puede robar por mazo vacío", () =>
            {
                var eng = M1Tests.NewGame(cat, 107, firstPlayer: 0);
                eng.State.Players[1].Mazo.Cards.Clear();
                eng.EndTurn(); // pasa a P1, que intenta robar de mazo vacío
                TestRunner.IsTrue(eng.State.IsOver, "partida terminó");
                TestRunner.AreEqual(0, eng.State.Winner!.Value, "gana P0");
                TestRunner.AreEqual(VictoryId.II, eng.State.WinReason!.Value);
            });

            t.Case("M2 ActivateDia (gratis) avanza el contador y mueve a Retirados", () =>
            {
                var eng = M1Tests.NewGame(cat, 108);
                var p = eng.State.Active;
                p.Tierras.Add(eng.NewInstance(cat.Get("t03"), p.Id)); // dia1 exige 1 TIERRA
                int retiradosAntes = p.Retirados.Count;
                TestRunner.IsTrue(eng.ActivateDia(useFree: true).Ok, "activar dia1 gratis");
                TestRunner.AreEqual(2, p.DiaActual, "ahora toca dia2");
                TestRunner.AreEqual(6, p.PilaDia.Count, "quedan 6 en la pila");
                TestRunner.AreEqual(retiradosAntes + 1, p.Retirados.Count);
                TestRunner.IsTrue(!eng.ActivateDia(useFree: true).Ok, "no 2ª activación gratis");
            });

            t.Case("M2 descarte al límite de mano en Entrega", () =>
            {
                var eng = M1Tests.NewGame(cat, 109, firstPlayer: 0);
                var p = eng.State.Players[0];
                p.Mano.Add(eng.NewInstance(cat.Get("sh04"), 0));
                p.Mano.Add(eng.NewInstance(cat.Get("sh04"), 0)); // mano = 9
                TestRunner.IsTrue(eng.EndTurn().Ok, "endturn con autodescarte");
                TestRunner.AreEqual(7, eng.State.Players[0].Mano.Count, "mano recortada a 7");
            });
        }
    }
}
