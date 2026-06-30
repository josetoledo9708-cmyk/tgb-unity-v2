using System.Linq;
using Game.Core.Effects;
using Game.Core.Engine;
using Game.Core.Model;

namespace Game.Core.Tests
{
    public static class M7Tests
    {
        private static GameEngine NewGameFx(CardCatalog cat, ulong seed, string h0 = "h1", int first = 0)
        {
            var eng = M1Tests.NewGame(cat, seed, h0: h0, firstPlayer: first);
            eng.Effects = CardEffects.BuildResolver();
            return eng;
        }

        public static void Run(TestRunner t, CardCatalog cat)
        {
            t.Case("M7 cobertura: el registry cubre >=100 cartas; básicas sin efecto", () =>
            {
                var reg = CardEffects.BuildRegistry();
                var ids = reg.CoveredCardIds();
                TestRunner.IsTrue(ids.Count >= 100, $"cubiertas={ids.Count}");
                TestRunner.IsTrue(!ids.Contains("t03"), "Ararat es básica (sin efecto)");
            });

            t.Case("M7 Babel AL_TAPEARSE bloquea el DÍA del rival", () =>
            {
                var eng = NewGameFx(cat, 600);
                var p = eng.State.Active;
                var babel = eng.NewInstance(cat.Get("t04"), p.Id); p.Tierras.Add(babel);
                eng.TapTierra(babel);
                TestRunner.AreEqual(1, eng.State.Opponent.DiaBlockedTurns, "rival no puede activar DÍA");
            });

            t.Case("M7 La Maldición de la Tierra anula el FD de las TIERRAs rivales", () =>
            {
                var eng = NewGameFx(cat, 601);
                var p = eng.State.Active; var opp = eng.State.Opponent; p.Fd = 5;
                var c05 = eng.NewInstance(cat.Get("c05"), p.Id); p.Mano.Add(c05);
                eng.PlayConcepto(c05, faceDown: false);
                TestRunner.AreEqual(1, opp.TierrasNoFdTurns, "1 turno sin FD");
            });

            t.Case("M7 Túnica de Colores da +2 dur al próximo SER jugado", () =>
            {
                var eng = NewGameFx(cat, 602);
                var p = eng.State.Active; p.Fd = 10;
                var tunica = eng.NewInstance(cat.Get("c17"), p.Id); p.Mano.Add(tunica);
                eng.PlayConcepto(tunica, faceDown: false);
                var abel = eng.NewInstance(cat.Get("sh04"), p.Id); p.Mano.Add(abel); // dur base 3
                eng.PlaySer(abel);
                TestRunner.AreEqual(5, abel.DurLeft, "3 + 2 de la Túnica");
            });

            t.Case("M7 El Jardín del Edén da +1 dur a los SER que entran", () =>
            {
                var eng = NewGameFx(cat, 603);
                var p = eng.State.Active; p.Fd = 10;
                p.Tierras.Add(eng.NewInstance(cat.Get("t01"), p.Id));
                var abel = eng.NewInstance(cat.Get("sh04"), p.Id); p.Mano.Add(abel); // dur base 3
                eng.PlaySer(abel);
                TestRunner.AreEqual(4, abel.DurLeft, "3 + 1 del Jardín");
            });

            t.Case("M7 Querubines: los efectos del rival quedan bloqueados", () =>
            {
                var eng = NewGameFx(cat, 604);
                var p = eng.State.Active; var opp = eng.State.Opponent; p.Fd = 10;
                var quer = eng.NewInstance(cat.Get("sd1"), p.Id); p.Mano.Add(quer);
                eng.PlaySer(quer);
                TestRunner.IsTrue(opp.EffectsBlockedTurns > 0, "flag de bloqueo");
                opp.Fd = 0;
                var ismael = eng.NewInstance(cat.Get("sh10"), opp.Id); opp.Seres.Add(ismael);
                eng.Fire(ismael, EffectTrigger.AlEntrar); // +1 FD, pero bloqueado
                TestRunner.AreEqual(0, opp.Fd, "efecto del rival no se aplicó");
            });

            t.Case("M7 Confusión de Lenguas deshabilita efectos activados", () =>
            {
                var eng = NewGameFx(cat, 605);
                var p = eng.State.Active; p.Fd = 10;
                var conf = eng.NewInstance(cat.Get("c13"), p.Id); p.Mano.Add(conf);
                eng.PlayConcepto(conf, faceDown: false);
                TestRunner.IsTrue(eng.State.ActivatedEffectsDisabled, "flag global");
                var sem = eng.NewInstance(cat.Get("sh06"), p.Id); p.Seres.Add(sem);
                int fdAntes = p.Fd; // Sem actCost=1; el +2 FD del efecto queda deshabilitado
                eng.ActivateSerEffect(sem);
                TestRunner.AreEqual(fdAntes - 1, p.Fd, "solo se pagó actCost; sin +2 del efecto");
            });

            t.Case("M7 ActivateResponse: trampa anula el próximo efecto del rival", () =>
            {
                var eng = NewGameFx(cat, 606, first: 0);
                var p0 = eng.State.Players[0]; var p1 = eng.State.Players[1];
                var trap = eng.NewInstance(cat.Get("c03"), 0); p0.Mano.Add(trap);
                eng.PlayConcepto(trap, faceDown: true); // P0 arma la trampa
                p0.Fd = 5;                              // FD reservado que persiste al turno rival
                eng.EndTurn();                          // ahora juega P1
                TestRunner.AreEqual(1, eng.State.ActivePlayer);
                TestRunner.IsTrue(eng.ActivateResponse(trap).Ok, "activar respuesta");
                TestRunner.IsTrue(p1.NegatedNextEffect, "el próximo efecto de P1 será anulado");
                p1.Fd = 3;
                var ismael = eng.NewInstance(cat.Get("sh10"), 1); p1.Mano.Add(ismael);
                eng.PlaySer(ismael); // AL_ENTRAR +1 FD, pero anulado
                TestRunner.AreEqual(0, p1.Fd, "efecto anulado: no ganó FD");
            });

            t.Case("M7 ResponseWindow: el defensor anula un efecto ofensivo automáticamente", () =>
            {
                var eng = NewGameFx(cat, 607, first: 0);
                var atk = eng.State.Players[0];   // atacante (activo)
                var def = eng.State.Players[1];   // defensor con trampa
                var trap = eng.NewInstance(cat.Get("c19"), def.Id); trap.FaceDown = true; // c19 niega cualquiera
                def.Concepto.Add(trap); def.Fd = 9;
                eng.ResponseWindow = (d, atkCard, category) =>
                    d == def.Id ? def.Concepto.Cards.FirstOrDefault(c => c.FaceDown) : null;

                int manoAntes = def.Mano.Count;                 // sa1 descartaría 1 al defensor
                var serpiente = eng.NewInstance(cat.Get("sa1"), atk.Id);
                eng.Fire(serpiente, EffectTrigger.AlEntrar);     // dispara ventana de respuesta
                TestRunner.AreEqual(manoAntes, def.Mano.Count, "el descarte se anuló por la trampa");
                TestRunner.IsTrue(!trap.FaceDown, "la trampa se reveló al activarse");
            });

            t.Case("M7 ResponseRules: categorías y condiciones de cada trampa", () =>
            {
                TestRunner.AreEqual(EffectCategory.ActivaDia, ResponseRules.CategoryOf("dia3", EffectTrigger.AlActivarElDia), "activar DÍA");
                TestRunner.AreEqual(EffectCategory.DestruyeTierra, ResponseRules.CategoryOf("sh03", EffectTrigger.AlEntrar), "Caín destruye TIERRA");
                TestRunner.AreEqual(EffectCategory.Otro, ResponseRules.CategoryOf("sa1", EffectTrigger.AlEntrar), "descarte genérico = Otro");
                TestRunner.IsTrue(ResponseRules.Applies("c19", EffectCategory.Otro), "c19 niega cualquiera");
                TestRunner.IsTrue(ResponseRules.Applies("c12", EffectCategory.DestruyeTierra), "c12 aplica a destruir TIERRA");
                TestRunner.IsTrue(!ResponseRules.Applies("c12", EffectCategory.Otro), "c12 NO aplica a un efecto cualquiera");
                TestRunner.IsTrue(ResponseRules.Applies("c29", EffectCategory.ActivaDia), "c29 cancela activar DÍA");
                TestRunner.IsTrue(!ResponseRules.Applies("c29", EffectCategory.DestruyeTierra), "c29 NO cancela destruir TIERRA");
            });

            t.Case("M7 José vuelve indestructibles a El Faraón y Egipto", () =>
            {
                var eng = NewGameFx(cat, 607, h0: "h7");
                var p = eng.State.Players[0]; eng.State.ActivePlayer = 0; p.Fd = 10;
                var egipto = eng.NewInstance(cat.Get("t12"), 0); p.Tierras.Add(egipto);
                p.Seres.Add(eng.NewInstance(cat.Get("sh18"), 0)); // El Faraón
                var jose = eng.NewInstance(cat.Get("sh16"), 0); p.Mano.Add(jose);
                eng.PlaySer(jose);
                TestRunner.IsTrue(egipto.Indestructible, "Egipto indestructible");
                TestRunner.IsTrue(!EffectApi.DestroyTierraByEffect(eng, egipto), "no se puede destruir");
            });

            t.Case("M7 La Piedra de Jacob coloca TIERRA sin consumir la jugada de TIERRA", () =>
            {
                var eng = NewGameFx(cat, 608);
                var p = eng.State.Active; p.Fd = 5;
                p.Mano.Cards.RemoveAll(x => x.Type == CardType.Tierra); // dejar solo la inyectada
                p.Mano.Add(eng.NewInstance(cat.Get("t03"), p.Id));
                var piedra = eng.NewInstance(cat.Get("c40"), p.Id); p.Mano.Add(piedra);
                int tierrasAntes = p.Tierras.Count;
                eng.PlayConcepto(piedra, faceDown: false);
                TestRunner.AreEqual(tierrasAntes + 1, p.Tierras.Count, "TIERRA colocada");
                TestRunner.IsTrue(!p.TierraPlayedThisTurn, "no consumió la jugada de TIERRA del turno");
            });
        }
    }
}
