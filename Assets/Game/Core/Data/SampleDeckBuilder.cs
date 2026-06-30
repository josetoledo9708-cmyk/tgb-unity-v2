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

        // CONCEPTOs de búsqueda/cava, ordenados por potencia para HALLAR PIEZAS rápido:
        // tutores directos primero (traen SER/TIERRA), luego cava profunda, luego robo simple.
        // Se incluye un par de cartas de RESPUESTA (trampas) para probar sus interacciones.
        private static readonly string[] Spells =
        {
            "c35", "c24", "c08", "c39",   // tutores: SER a mano / TIERRA al campo (agarran piezas)
            "c19", "c29",                 // RESPUESTA: niega cualquier efecto / cancela activar DÍA rival
            "c41", "c30", "c28",          // cava: mira 3-5 y toma SER / carta
            "c09", "c04", "c01", "c38", "c16", "c27" // búsqueda TIERRA / robo simple
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

            // 2) Rampa de TIERRA: ~50% del espacio restante (FD suficiente, deja sitio a la cava).
            int landTarget = ids.Count + (size - ids.Count) * 5 / 10;
            CycleFill(Lands, landTarget);

            // 3) CONCEPTOs de cava/tutor hasta llenar (densidad alta para hallar piezas rápido).
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
