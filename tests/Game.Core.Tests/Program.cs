using System;
using System.IO;
using Game.Core.Data;
using Game.Core.Model;

namespace Game.Core.Tests
{
    public static class Program
    {
        public static int Main()
        {
            string json = File.ReadAllText(LocateCatalog());
            var cat = CatalogLoader.FromJson(json);
            var t = new TestRunner();

            Console.WriteLine("CatalogLoader (M0)");

            t.Case("conteo por tipo coincide con catalogo v3-rev1", () =>
            {
                TestRunner.AreEqual(7, cat.CountByType(CardType.Dia), "DIA");
                TestRunner.AreEqual(20, cat.CountByType(CardType.Tierra), "TIERRA");
                TestRunner.AreEqual(9, cat.CountByType(CardType.SerDivino), "SER_DIVINO");
                TestRunner.AreEqual(31, cat.CountByType(CardType.SerHumano), "SER_HUMANO");
                TestRunner.AreEqual(5, cat.CountByType(CardType.SerAnimal), "SER_ANIMAL");
                TestRunner.AreEqual(42, cat.CountByType(CardType.Concepto), "CONCEPTO");
            });

            t.Case("total de cartas jugables = 114", () =>
                TestRunner.AreEqual(114, cat.Cards.Count));

            t.Case("7 historias y 7 dias", () =>
            {
                TestRunner.AreEqual(7, cat.Historias.Count, "historias");
                TestRunner.AreEqual(7, cat.Dias.Count, "dias");
            });

            t.Case("t21 (stub Peniel) fue eliminado", () =>
                TestRunner.IsTrue(!cat.TryGet("t21", out _), "t21 no debe existir"));

            t.Case("t15 Peniel sigue presente", () =>
            {
                TestRunner.IsTrue(cat.TryGet("t15", out var p), "t15 debe existir");
                TestRunner.AreEqual("Peniel", p.Nombre);
            });

            t.Case("h5 usa modo_victoria on_piece_play; el resto field_at_entrega", () =>
            {
                foreach (var h in cat.Historias)
                {
                    var esperado = h.Id == "h5"
                        ? VictoryMode.OnPiecePlay
                        : VictoryMode.FieldAtEntrega;
                    TestRunner.AreEqual(esperado, h.ModoVictoria, $"modo de {h.Id}");
                }
            });

            t.Case("cada HISTORIA tiene exactamente 5 piezas", () =>
            {
                foreach (var h in cat.Historias)
                    TestRunner.AreEqual(5, h.Piezas.Count, $"piezas de {h.Id}");
            });

            t.Case("campos clave parseados: Sodoma fd=2 turns_left=3", () =>
            {
                var s = cat.Get("t08");
                TestRunner.AreEqual(2, s.Fd!.Value, "fd");
                TestRunner.AreEqual(3, s.TurnsLeft!.Value, "turns_left");
                TestRunner.IsTrue(s.Especial, "especial");
            });

            t.Case("SER parsea coste/dur/actCost: Adan", () =>
            {
                var a = cat.Get("sh01");
                TestRunner.AreEqual(3, a.Coste!.Value, "coste");
                TestRunner.AreEqual(4, a.Dur!.Value, "dur");
                TestRunner.AreEqual(2, a.ActCost!.Value, "actCost");
            });

            t.Case("piezas de cada HISTORIA existen como carta (por nombre)", () =>
            {
                var nombres = new System.Collections.Generic.HashSet<string>();
                foreach (var c in cat.Cards.Values) nombres.Add(c.Nombre);
                foreach (var h in cat.Historias)
                    foreach (var pieza in h.Piezas)
                        TestRunner.IsTrue(nombres.Contains(pieza),
                            $"pieza '{pieza}' de {h.Id} no existe como carta");
            });

            return t.Summarize();
        }

        /// <summary>Sube directorios hasta encontrar data/catalogo.v3.json.</summary>
        private static string LocateCatalog()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "data", "catalogo.v3.json");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new FileNotFoundException("No se encontró data/catalogo.v3.json subiendo desde "
                + AppContext.BaseDirectory);
        }
    }
}
