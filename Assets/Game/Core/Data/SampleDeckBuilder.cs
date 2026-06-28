using System.Collections.Generic;
using System.Linq;
using Game.Core.Model;

namespace Game.Core.Data
{
    /// <summary>
    /// Construye un mazo válido de muestra para una HISTORIA: incluye sus 5 piezas y rellena
    /// con cartas jugables hasta el tamaño pedido, respetando el máximo de copias.
    /// Útil para tests y para la demo de Unity.
    /// </summary>
    public static class SampleDeckBuilder
    {
        public static DeckDefinition Build(CardCatalog cat, string historiaId, int size = 40)
        {
            var historia = cat.FindHistoria(historiaId)!;
            var ids = new List<string>();
            var counts = new Dictionary<string, int>();

            void TryAdd(string id)
            {
                counts.TryGetValue(id, out var c);
                if (c >= DeckValidator.MaxCopies) return;
                ids.Add(id);
                counts[id] = c + 1;
            }

            foreach (var pieza in historia.Piezas)
                TryAdd(cat.Cards.Values.First(x => x.Nombre == pieza).Id);

            var fillers = cat.Cards.Values
                .Where(c => c.Type != CardType.Dia && c.Type != CardType.Historia)
                .OrderBy(c => c.Id)
                .ToList();

            int fi = 0;
            while (ids.Count < size && fillers.Count > 0)
            {
                TryAdd(fillers[fi % fillers.Count].Id);
                fi++;
                if (fi > fillers.Count * DeckValidator.MaxCopies + 10) break;
            }

            return new DeckDefinition(ids, historiaId);
        }
    }
}
