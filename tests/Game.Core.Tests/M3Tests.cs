using System.Linq;
using Game.Core.Engine;
using Game.Core.Model;

namespace Game.Core.Tests
{
    public static class M3Tests
    {
        public static void Run(TestRunner t, CardCatalog cat)
        {
            t.Case("M3 condición DÍA1 exige 1 TIERRA", () =>
            {
                var eng = M1Tests.NewGame(cat, 200);
                var p = eng.State.Active;
                TestRunner.IsTrue(!DiaConditions.Met(p, 1), "0 tierras: no cumple");
                p.Tierras.Add(eng.NewInstance(cat.Get("t03"), p.Id));
                TestRunner.IsTrue(DiaConditions.Met(p, 1), "1 tierra: cumple");
            });

            t.Case("M3 ActivateDia falla si no se cumple la condición", () =>
            {
                var eng = M1Tests.NewGame(cat, 201);
                // 0 tierras -> dia1 no se puede
                TestRunner.IsTrue(!eng.ActivateDia(useFree: true).Ok, "sin tierra debe fallar");
            });

            t.Case("M3 condición DÍA4: 3 TIERRAs + 1 SER coste>=3", () =>
            {
                var eng = M1Tests.NewGame(cat, 202);
                var p = eng.State.Active;
                for (int i = 0; i < 3; i++) p.Tierras.Add(eng.NewInstance(cat.Get("t03"), p.Id));
                TestRunner.IsTrue(!DiaConditions.Met(p, 4), "falta SER coste>=3");
                p.Seres.Add(eng.NewInstance(cat.Get("sh05"), p.Id)); // Noé coste 5
                TestRunner.IsTrue(DiaConditions.Met(p, 4), "ahora cumple");
            });

            t.Case("M3 Victoria I al activar el DÍA 7", () =>
            {
                var eng = M1Tests.NewGame(cat, 203);
                var p = eng.State.Active;
                for (int n = 1; n <= 6; n++) p.DiasActivados.Add(n);
                p.DiaActual = 7;
                for (int i = 0; i < 3; i++) p.Tierras.Add(eng.NewInstance(cat.Get("t03"), p.Id));
                p.Seres.Add(eng.NewInstance(cat.Get("sh04"), p.Id)); // coste 2 (<=2)
                p.Seres.Add(eng.NewInstance(cat.Get("sh05"), p.Id)); // coste 5 (>=3)
                TestRunner.IsTrue(DiaConditions.Met(p, 7), "condición dia7 cumplida");
                TestRunner.IsTrue(eng.ActivateDia(useFree: true).Ok, "activar dia7");
                TestRunner.IsTrue(eng.State.IsOver, "partida terminó");
                TestRunner.AreEqual(p.Id, eng.State.Winner!.Value);
                TestRunner.AreEqual(VictoryId.I, eng.State.WinReason!.Value);
            });

            t.Case("M3 Victoria III (field_at_entrega) con las 5 piezas de h1", () =>
            {
                var eng = M1Tests.NewGame(cat, 204, h0: "h1");
                var p = eng.State.Players[0];
                // piezas h1: Jardín(t01), Árbol(t02) [TIERRA]; Adán(sh01), Eva(sh02), Serpiente(sa1) [SER]
                p.Tierras.Add(eng.NewInstance(cat.Get("t01"), 0));
                p.Tierras.Add(eng.NewInstance(cat.Get("t02"), 0));
                p.Seres.Add(eng.NewInstance(cat.Get("sh01"), 0));
                p.Seres.Add(eng.NewInstance(cat.Get("sh02"), 0));
                p.Seres.Add(eng.NewInstance(cat.Get("sa1"), 0));
                eng.EndTurn();
                TestRunner.IsTrue(eng.State.IsOver, "debió ganar en Entrega");
                TestRunner.AreEqual(0, eng.State.Winner!.Value);
                TestRunner.AreEqual(VictoryId.III, eng.State.WinReason!.Value);
            });

            t.Case("M3 Victoria III (field_at_entrega) NO con 4 piezas", () =>
            {
                var eng = M1Tests.NewGame(cat, 205, h0: "h1");
                var p = eng.State.Players[0];
                p.Tierras.Add(eng.NewInstance(cat.Get("t01"), 0));
                p.Tierras.Add(eng.NewInstance(cat.Get("t02"), 0));
                p.Seres.Add(eng.NewInstance(cat.Get("sh01"), 0));
                p.Seres.Add(eng.NewInstance(cat.Get("sh02"), 0));
                // falta La Serpiente
                eng.EndTurn();
                TestRunner.IsTrue(!eng.State.IsOver, "no debe ganar con 4 piezas");
            });

            t.Case("M3 Victoria III (on_piece_play, h5) al jugar La Maldición", () =>
            {
                var eng = M1Tests.NewGame(cat, 206, h0: "h5");
                var p = eng.State.Players[0];
                // 4 permanentes: Jardín(t01), Nod(t14) [TIERRA]; Caín(sh03), Abel(sh04) [SER]
                p.Tierras.Add(eng.NewInstance(cat.Get("t01"), 0));
                p.Tierras.Add(eng.NewInstance(cat.Get("t14"), 0));
                p.Seres.Add(eng.NewInstance(cat.Get("sh03"), 0));
                p.Seres.Add(eng.NewInstance(cat.Get("sh04"), 0));
                // 5ª pieza en mano: La Maldición de la Tierra (c05)
                var maldicion = eng.NewInstance(cat.Get("c05"), 0);
                p.Mano.Add(maldicion);
                p.Fd = 5;
                var res = eng.PlayConcepto(maldicion, faceDown: false);
                TestRunner.IsTrue(res.Ok, "jugar concepto ok");
                TestRunner.IsTrue(eng.State.IsOver, "debió ganar al jugar la 5ª pieza");
                TestRunner.AreEqual(VictoryId.III, eng.State.WinReason!.Value);
            });

            t.Case("M3 h5 NO gana si falta una de las 4 piezas permanentes", () =>
            {
                var eng = M1Tests.NewGame(cat, 207, h0: "h5");
                var p = eng.State.Players[0];
                p.Tierras.Add(eng.NewInstance(cat.Get("t01"), 0));
                p.Seres.Add(eng.NewInstance(cat.Get("sh03"), 0));
                p.Seres.Add(eng.NewInstance(cat.Get("sh04"), 0));
                // falta Nod (t14)
                var maldicion = eng.NewInstance(cat.Get("c05"), 0);
                p.Mano.Add(maldicion);
                p.Fd = 5;
                eng.PlayConcepto(maldicion, faceDown: false);
                TestRunner.IsTrue(!eng.State.IsOver, "no debe ganar sin las 4 piezas");
            });
        }
    }
}
