using System.Collections.Generic;
using System.Linq;
using Game.Core.Model;

namespace Game.Core.Data
{
    /// <summary>
    /// Construye un mazo de muestra jugable para una HISTORIA: incluye sus 5 piezas y rellena
    /// con un núcleo curado (TIERRAs para FD, SER baratos de robo/FD, CONCEPTOs de robo/búsqueda
    /// y algunos SER fuertes), respetando 40 cartas y el máximo de copias.
    /// </summary>
    public static class SampleDeckBuilder
    {
        // Núcleo en orden de prioridad; se añade ciclando (1 de cada, luego 2ª, luego 3ª copia).
        private static readonly string[] Core =
        {
            "t03", "t06", "t07",                         // TIERRA básica (FD fiable)
            "t17", "t20", "t12", "t10", "t05",           // TIERRA especial
            "sa2", "sa3", "sh10", "sh06", "sh06b", "sh04", "sh22", // SER barato: robo/FD
            "c04", "c09", "c24", "c27", "c16", "c38", "c01",       // CONCEPTO: robo/busca/revive
            "sh23", "sh18", "sh19", "sh05"               // SER fuerte
        };

        public static DeckDefinition Build(CardCatalog cat, string historiaId, int size = 40)
        {
            var historia = cat.FindHistoria(historiaId)!;
            var ids = new List<string>();
            var counts = new Dictionary<string, int>();

            bool Add(string id)
            {
                if (ids.Count >= size || !cat.TryGet(id, out var def)) return false;
                if (def.Type == CardType.Dia || def.Type == CardType.Historia) return false;
                counts.TryGetValue(id, out var c);
                if (c >= DeckValidator.MaxCopies) return false;
                ids.Add(id);
                counts[id] = c + 1;
                return true;
            }

            // 1) Las 5 piezas (1 copia cada una).
            foreach (var pieza in historia.Piezas)
                Add(cat.Cards.Values.First(x => x.Nombre == pieza).Id);

            // 2) Núcleo curado, ciclando hasta llenar.
            bool progressed = true;
            while (ids.Count < size && progressed)
            {
                progressed = false;
                foreach (var id in Core)
                {
                    if (ids.Count >= size) break;
                    if (Add(id)) progressed = true;
                }
            }

            // 3) Relleno de seguridad (por si el núcleo no alcanza): cartas baratas.
            if (ids.Count < size)
                foreach (var def in cat.Cards.Values
                             .Where(x => x.Type != CardType.Dia && x.Type != CardType.Historia)
                             .OrderBy(x => x.Coste ?? 0))
                    while (ids.Count < size && Add(def.Id)) { }

            return new DeckDefinition(ids, historiaId);
        }
    }
}
