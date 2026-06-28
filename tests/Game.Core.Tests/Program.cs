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
            CardCatalog cat = CatalogLoader.FromJson(json);
            var t = new TestRunner();

            Console.WriteLine("== The Great Book — Core tests ==");
            M0Tests.Run(t, cat);
            M1Tests.Run(t, cat);
            M2Tests.Run(t, cat);
            M3Tests.Run(t, cat);
            M4Tests.Run(t, cat);
            M5Tests.Run(t, cat);
            M6Tests.Run(t, cat);
            M7Tests.Run(t, cat);

            return t.Summarize();
        }

        private static string LocateCatalog()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "data", "catalogo.v3.json");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            throw new FileNotFoundException(
                "No se encontró data/catalogo.v3.json desde " + AppContext.BaseDirectory);
        }
    }
}
