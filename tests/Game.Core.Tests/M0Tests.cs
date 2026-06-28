using System.Collections.Generic;
using Game.Core.Model;

namespace Game.Core.Tests
{
    public static class M0Tests
    {
        public static void Run(TestRunner t, CardCatalog cat)
        {
            t.Case("M0 conteo por tipo coincide con catalogo v3-rev1", () =>
            {
                TestRunner.AreEqual(7, cat.CountByType(CardType.Dia), "DIA");
                TestRunner.AreEqual(20, cat.CountByType(CardType.Tierra), "TIERRA");
                TestRunner.AreEqual(9, cat.CountByType(CardType.SerDivino), "SER_DIVINO");
                TestRunner.AreEqual(31, cat.CountByType(CardType.SerHumano), "SER_HUMANO");
                TestRunner.AreEqual(5, cat.CountByType(CardType.SerAnimal), "SER_ANIMAL");
                TestRunner.AreEqual(42, cat.CountByType(CardType.Concepto), "CONCEPTO");
            });

            t.Case("M0 total de cartas jugables = 114", () =>
                TestRunner.AreEqual(114, cat.Cards.Count));

            t.Case("M0 7 historias y 7 dias", () =>
            {
                TestRunner.AreEqual(7, cat.Historias.Count, "historias");
                TestRunner.AreEqual(7, cat.Dias.Count, "dias");
            });

            t.Case("M0 t21 (stub Peniel) fue eliminado", () =>
                TestRunner.IsTrue(!cat.TryGet("t21", out _), "t21 no debe existir"));

            t.Case("M0 h5 on_piece_play; el resto field_at_entrega", () =>
            {
                foreach (var h in cat.Historias)
                {
                    var esperado = h.Id == "h5" ? VictoryMode.OnPiecePlay : VictoryMode.FieldAtEntrega;
                    TestRunner.AreEqual(esperado, h.ModoVictoria, $"modo de {h.Id}");
                }
            });

            t.Case("M0 cada HISTORIA tiene 5 piezas que existen como carta", () =>
            {
                var nombres = new HashSet<string>();
                foreach (var c in cat.Cards.Values) nombres.Add(c.Nombre);
                foreach (var h in cat.Historias)
                {
                    TestRunner.AreEqual(5, h.Piezas.Count, $"piezas de {h.Id}");
                    foreach (var pieza in h.Piezas)
                        TestRunner.IsTrue(nombres.Contains(pieza), $"pieza '{pieza}' no existe");
                }
            });

            t.Case("M0 Sodoma fd=2 turns_left=3 especial", () =>
            {
                var s = cat.Get("t08");
                TestRunner.AreEqual(2, s.Fd!.Value, "fd");
                TestRunner.AreEqual(3, s.TurnsLeft!.Value, "turns_left");
                TestRunner.IsTrue(s.Especial, "especial");
            });
        }
    }
}
