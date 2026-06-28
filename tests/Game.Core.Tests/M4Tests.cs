using System.Linq;
using Game.Core.Effects;
using Game.Core.Engine;
using Game.Core.Model;

namespace Game.Core.Tests
{
    public static class M4Tests
    {
        private static GameEngine NewGameFx(CardCatalog cat, ulong seed, string h0 = "h1")
        {
            var eng = M1Tests.NewGame(cat, seed, h0: h0);
            eng.Effects = CardEffects.BuildResolver();
            return eng;
        }

        public static void Run(TestRunner t, CardCatalog cat)
        {
            t.Case("M4 Ismael AL_ENTRAR genera +1 FD", () =>
            {
                var eng = NewGameFx(cat, 300);
                var p = eng.State.Active; p.Fd = 3;
                var ismael = eng.NewInstance(cat.Get("sh10"), p.Id); p.Mano.Add(ismael);
                TestRunner.IsTrue(eng.PlaySer(ismael).Ok);
                TestRunner.AreEqual(1, p.Fd, "3 - coste3 + 1 = 1");
            });

            t.Case("M4 Abel AL_ENTRAR busca 'La Ofrenda de Abel' a la mano", () =>
            {
                var eng = NewGameFx(cat, 301);
                var p = eng.State.Active; p.Fd = 5;
                p.Mazo.Add(eng.NewInstance(cat.Get("c04"), p.Id)); // garantizar que está en mazo
                var abel = eng.NewInstance(cat.Get("sh04"), p.Id); p.Mano.Add(abel);
                TestRunner.IsTrue(eng.PlaySer(abel).Ok);
                TestRunner.IsTrue(p.Mano.Cards.Any(c => c.Def.Id == "c04"), "Ofrenda en mano");
            });

            t.Case("M4 Adán busca a Eva si no está; luego Eva roba con Adán en campo", () =>
            {
                var eng = NewGameFx(cat, 302);
                var p = eng.State.Active; p.Fd = 20;
                p.Mazo.Add(eng.NewInstance(cat.Get("sh02"), p.Id)); // Eva en mazo
                var adan = eng.NewInstance(cat.Get("sh01"), p.Id); p.Mano.Add(adan);
                eng.PlaySer(adan);
                var eva = p.Mano.Cards.First(c => c.Def.Id == "sh02"); // la buscada
                int manoAntes = p.Mano.Count;
                eng.PlaySer(eva); // Adán en campo -> roba 1
                TestRunner.IsTrue(p.Mano.Count >= manoAntes, "Eva con Adán roba");
            });

            t.Case("M4 El Cuervo EFECTO_ACTIVADO roba 1", () =>
            {
                var eng = NewGameFx(cat, 303);
                var p = eng.State.Active; p.Fd = 5;
                var cuervo = eng.NewInstance(cat.Get("sa2"), p.Id); p.Seres.Add(cuervo);
                int antes = p.Mano.Count;
                TestRunner.IsTrue(eng.ActivateSerEffect(cuervo).Ok);
                TestRunner.AreEqual(antes + 1, p.Mano.Count);
            });

            t.Case("M4 La Copa de Benjamín: dueño roba 2, rival roba 1", () =>
            {
                var eng = NewGameFx(cat, 304);
                var p = eng.State.Active; var opp = eng.State.Opponent; p.Fd = 5;
                var copa = eng.NewInstance(cat.Get("c27"), p.Id); p.Mano.Add(copa);
                int pm = p.Mano.Count, om = opp.Mano.Count;
                TestRunner.IsTrue(eng.PlayConcepto(copa, faceDown: false).Ok);
                // p jugó la copa (mano -1) y robó 2 => neto +1; opp +1
                TestRunner.AreEqual(pm + 1, p.Mano.Count, "dueño neto +1");
                TestRunner.AreEqual(om + 1, opp.Mano.Count, "rival +1");
            });

            t.Case("M4 Faraón EFECTO_ACTIVADO genera FD = mitad de TIERRAs (máx 4)", () =>
            {
                var eng = NewGameFx(cat, 305);
                var p = eng.State.Active;
                for (int i = 0; i < 4; i++) p.Tierras.Add(eng.NewInstance(cat.Get("t03"), p.Id));
                var faraon = eng.NewInstance(cat.Get("sh18"), p.Id); p.Seres.Add(faraon);
                p.Fd = 4; // actCost del Faraón = 4
                TestRunner.IsTrue(eng.ActivateSerEffect(faraon).Ok);
                TestRunner.AreEqual(2, p.Fd, "4-4 + (4/2)=2");
            });

            t.Case("M4 Melquisedec AL_ENTRAR activa el DÍA gratis (condición cumplida)", () =>
            {
                var eng = NewGameFx(cat, 306);
                var p = eng.State.Active; p.Fd = 4;
                p.Tierras.Add(eng.NewInstance(cat.Get("t03"), p.Id)); // dia1 exige 1 TIERRA
                var melq = eng.NewInstance(cat.Get("sh19"), p.Id); p.Mano.Add(melq);
                TestRunner.IsTrue(eng.PlaySer(melq).Ok);
                TestRunner.AreEqual(2, p.DiaActual, "dia1 activado -> toca dia2");
                TestRunner.IsTrue(p.DiaFreeUsed, "usó la activación gratuita");
            });

            t.Case("M4 Soplo de Vida revive un SER de Retirados al campo", () =>
            {
                var eng = NewGameFx(cat, 307);
                var p = eng.State.Active; p.Fd = 5;
                var muerto = eng.NewInstance(cat.Get("sh06"), p.Id); p.Retirados.Add(muerto);
                var soplo = eng.NewInstance(cat.Get("c01"), p.Id); p.Mano.Add(soplo);
                TestRunner.IsTrue(eng.PlayConcepto(soplo, faceDown: false).Ok);
                TestRunner.IsTrue(p.Seres.Cards.Any(c => c.Def.Id == "sh06"), "Sem revivido en campo");
            });

            t.Case("M4 registry: carta sin handler no rompe (Ararat)", () =>
            {
                var eng = NewGameFx(cat, 308);
                var p = eng.State.Active;
                var ararat = eng.NewInstance(cat.Get("t03"), p.Id); p.Mano.Add(ararat);
                TestRunner.IsTrue(eng.PlayTierra(ararat).Ok, "tierra sin efecto se juega igual");
            });
        }
    }
}
