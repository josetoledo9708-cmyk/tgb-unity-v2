using System.Linq;
using Game.Core.Effects;
using Game.Core.Engine;
using Game.Core.Model;

namespace Game.Core.Tests
{
    public static class M5Tests
    {
        private static GameEngine NewGameFx(CardCatalog cat, ulong seed, string h0 = "h1")
        {
            var eng = M1Tests.NewGame(cat, seed, h0: h0);
            eng.Effects = CardEffects.BuildResolver();
            return eng;
        }

        public static void Run(TestRunner t, CardCatalog cat)
        {
            t.Case("M5 El Diluvio destruye todos los SER; La Rama queda bloqueada el turno", () =>
            {
                var eng = NewGameFx(cat, 400);
                var p = eng.State.Active; var opp = eng.State.Opponent; p.Fd = 10;
                p.Seres.Add(eng.NewInstance(cat.Get("sh10"), p.Id));
                opp.Seres.Add(eng.NewInstance(cat.Get("sh10"), opp.Id));
                var diluvio = eng.NewInstance(cat.Get("c07"), p.Id); p.Mano.Add(diluvio);
                TestRunner.IsTrue(eng.PlayConcepto(diluvio, faceDown: false).Ok);
                TestRunner.AreEqual(0, p.Seres.Count, "SER propios destruidos");
                TestRunner.AreEqual(0, opp.Seres.Count, "SER rivales destruidos");
                TestRunner.IsTrue(p.DiluvioUsed, "flag diluvioUsed");
                var rama = eng.NewInstance(cat.Get("c08"), p.Id); p.Mano.Add(rama);
                TestRunner.IsTrue(!eng.PlayConcepto(rama, faceDown: false).Ok, "Rama bloqueada");
            });

            t.Case("M5 Sacrificio: solo una carta de sacrificio por turno", () =>
            {
                var eng = NewGameFx(cat, 401);
                var p = eng.State.Active; p.Fd = 10;
                p.Seres.Add(eng.NewInstance(cat.Get("sh10"), p.Id)); // algo que sacrificar
                var isaac = eng.NewInstance(cat.Get("c15"), p.Id); p.Mano.Add(isaac);
                TestRunner.IsTrue(eng.PlayConcepto(isaac, faceDown: false).Ok, "Sacrificio de Isaac ok");
                TestRunner.IsTrue(p.SacrificioUsed, "flag sacrificioUsed");
                var prueba = eng.NewInstance(cat.Get("c37"), p.Id); p.Mano.Add(prueba);
                TestRunner.IsTrue(!eng.PlayConcepto(prueba, faceDown: false).Ok, "2º sacrificio bloqueado");
            });

            t.Case("M5 Lot AL_ENTRAR activa tierraProtected", () =>
            {
                var eng = NewGameFx(cat, 402);
                var p = eng.State.Active; p.Fd = 5;
                var lot = eng.NewInstance(cat.Get("sh09"), p.Id); p.Mano.Add(lot);
                TestRunner.IsTrue(eng.PlaySer(lot).Ok);
                TestRunner.IsTrue(p.TierraProtected, "Lot protege TIERRAs");
            });

            t.Case("M5 La Destrucción de Sodoma destruye TIERRA rival y dispara AL_SER_DESTRUIDA", () =>
            {
                var eng = NewGameFx(cat, 403);
                var p = eng.State.Players[0]; var opp = eng.State.Players[1];
                eng.State.ActivePlayer = 0; p.Fd = 5;
                var sodoma = eng.NewInstance(cat.Get("t08"), opp.Id); opp.Tierras.Add(sodoma);
                var carta = eng.NewInstance(cat.Get("c11"), 0); p.Mano.Add(carta);
                int handConCarta = p.Mano.Count; // incluye c11 en mano
                TestRunner.IsTrue(eng.PlayConcepto(carta, faceDown: false).Ok);
                TestRunner.IsTrue(!opp.Tierras.Cards.Contains(sodoma), "Sodoma destruida");
                // -1 (juega c11) + 2 (Sodoma hace robar 2 a su rival P0) = neto +1
                TestRunner.AreEqual(handConCarta + 1, p.Mano.Count, "rival de Sodoma robó 2");
            });

            t.Case("M5 protección de Lot impide destruir TIERRA por efecto", () =>
            {
                var eng = NewGameFx(cat, 404);
                var p = eng.State.Players[0]; var opp = eng.State.Players[1];
                eng.State.ActivePlayer = 0; p.Fd = 5;
                var sodoma = eng.NewInstance(cat.Get("t08"), opp.Id); opp.Tierras.Add(sodoma);
                opp.Seres.Add(eng.NewInstance(cat.Get("sh09"), opp.Id)); // Lot en campo rival
                var carta = eng.NewInstance(cat.Get("c11"), 0); p.Mano.Add(carta);
                eng.PlayConcepto(carta, faceDown: false);
                TestRunner.IsTrue(opp.Tierras.Cards.Contains(sodoma), "Lot protege: Sodoma sigue");
            });

            t.Case("M5 Benjamín regresa al mazo al agotar duración; José hace robar 1", () =>
            {
                var eng = NewGameFx(cat, 405, h0: "h7");
                var p = eng.State.Players[0];
                var benja = eng.NewInstance(cat.Get("sh17"), 0); benja.DurLeft = 1; p.Seres.Add(benja);
                p.Seres.Add(eng.NewInstance(cat.Get("sh16"), 0)); // José
                int manoAntes = p.Mano.Count;
                eng.EndTurn(); // -> P1 turno 2
                eng.EndTurn(); // -> P0 turno 3 (Preludio: Benjamín dur 1->0)
                TestRunner.IsTrue(p.Mazo.Cards.Any(c => c.Def.Id == "sh17"), "Benjamín en el mazo");
                TestRunner.IsTrue(!p.Retirados.Cards.Any(c => c.Def.Id == "sh17"), "no en Retirados");
                TestRunner.IsTrue(p.Mano.Count > manoAntes, "José hizo robar al regresar");
            });

            t.Case("M5 Los Ángeles de Sodoma destruyen 2 TIERRAs", () =>
            {
                var eng = NewGameFx(cat, 406);
                var p = eng.State.Players[0]; var opp = eng.State.Players[1];
                eng.State.ActivePlayer = 0; p.Fd = 10;
                opp.Tierras.Add(eng.NewInstance(cat.Get("t03"), opp.Id));
                opp.Tierras.Add(eng.NewInstance(cat.Get("t06"), opp.Id));
                int antes = opp.Tierras.Count;
                var angeles = eng.NewInstance(cat.Get("sd3"), 0); p.Mano.Add(angeles);
                TestRunner.IsTrue(eng.PlaySer(angeles).Ok);
                TestRunner.AreEqual(antes - 2, opp.Tierras.Count, "2 TIERRAs destruidas");
            });

            t.Case("M5 Caín AL_SALIR envía La Tierra de Nod a Retirados", () =>
            {
                var eng = NewGameFx(cat, 407, h0: "h5");
                var p = eng.State.Players[0];
                var cain = eng.NewInstance(cat.Get("sh03"), 0); cain.DurLeft = 1; p.Seres.Add(cain);
                var nod = eng.NewInstance(cat.Get("t14"), 0); p.Tierras.Add(nod);
                eng.EndTurn(); // P1 turno 2
                eng.EndTurn(); // P0 turno 3: Caín decae -> AL_SALIR
                TestRunner.IsTrue(!p.Tierras.Cards.Contains(nod), "Nod salió del campo");
                TestRunner.IsTrue(p.Retirados.Cards.Contains(nod), "Nod en Retirados");
            });
        }
    }
}
