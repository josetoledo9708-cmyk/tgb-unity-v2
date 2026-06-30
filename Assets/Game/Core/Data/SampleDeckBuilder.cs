using System.Collections.Generic;
using System.Linq;
using Game.Core.Model;

namespace Game.Core.Data
{
    /// <summary>
    /// Construye un mazo jugable y enfocado en GANAR por una HISTORIA: incluye varias copias de
    /// sus 5 piezas y rellena con rampa de TIERRA (FD) + CONCEPTOs de robo/búsqueda para encontrar
    /// las piezas. Importante: NO mete SER "de relleno", porque solo hay 3 ranuras SER y deben
    /// quedar libres para las piezas SER de la historia.
    /// </summary>
    public static class SampleDeckBuilder
    {
        // TIERRAs neutrales para FD (no son piezas de ninguna historia clave): rampa fiable.
        private static readonly string[] Lands =
        {
            "t17", "t18", "t19", "t20",   // Ríos Pisón/Gehón/Tigris/Éufrates (FD 1)
            "t05", "t07", "t11",          // Canaán / Salem / Macpelá
            "t14", "t15", "t16"           // Nod / Peniel / Dotán
        };

        // CONCEPTOs de robo/búsqueda/recuperación para cavar hacia las piezas.
        private static readonly string[] Spells =
        {
            "c04", "c09", "c24", "c01", "c38", "c16", "c27"
        };

        public static DeckDefinition Build(CardCatalog cat, string historiaId, int size = 40, int pieceCopies = 3)
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

            void CycleFill(IEnumerable<string> pool, int target)
            {
                bool progressed = true;
                while (ids.Count < target && progressed)
                {
                    progressed = false;
                    foreach (var id in pool)
                    {
                        if (ids.Count >= target) break;
                        if (Add(id)) progressed = true;
                    }
                }
            }

            // 1) Las 5 piezas (pieceCopies copias de cada una). Prioridad máxima.
            foreach (var pieza in historia.Piezas)
            {
                var id = cat.Cards.Values.First(x => x.Nombre == pieza).Id;
                for (int k = 0; k < pieceCopies; k++) Add(id);
            }

            // 2) Rampa de TIERRA: ~60% del espacio restante para asegurar FD.
            int landTarget = ids.Count + (size - ids.Count) * 6 / 10;
            CycleFill(Lands, landTarget);

            // 3) CONCEPTOs de robo/búsqueda hasta llenar.
            CycleFill(Spells, size);

            // 4) Relleno de seguridad: más TIERRA (nunca SER de relleno).
            CycleFill(Lands, size);

            // 5) Último recurso: cualquier carta barata que no sea DÍA/HISTORIA.
            if (ids.Count < size)
                foreach (var def in cat.Cards.Values
                             .Where(x => x.Type != CardType.Dia && x.Type != CardType.Historia)
                             .OrderBy(x => x.Coste ?? 0))
                    while (ids.Count < size && Add(def.Id)) { }

            return new DeckDefinition(ids, historiaId);
        }
    }
}
