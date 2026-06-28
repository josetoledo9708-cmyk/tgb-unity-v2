using System.Collections.Generic;
using System.Linq;
using Game.Core.Data;
using Game.Core.Model;

namespace Game.Core.Tests
{
    /// <summary>Construye mazos válidos de prueba a partir del catálogo.</summary>
    public static class TestDecks
    {
        public static DeckDefinition BuildSample(CardCatalog cat, string historiaId, int size = 40)
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

            // 1) Una copia de cada pieza (por nombre -> id).
            foreach (var pieza in historia.Piezas)
            {
                var card = cat.Cards.Values.First(x => x.Nombre == pieza);
                TryAdd(card.Id);
            }

            // 2) Rellenar con cartas jugables hasta 'size' (máx 3 por id).
            var fillers = cat.Cards.Values
                .Where(c => c.Type != CardType.Dia && c.Type != CardType.Historia)
                .OrderBy(c => c.Id)
                .ToList();

            int fi = 0;
            while (ids.Count < size && fillers.Count > 0)
            {
                var card = fillers[fi % fillers.Count];
                TryAdd(card.Id);
                fi++;
                if (fi > fillers.Count * DeckValidator.MaxCopies + 10) break; // guarda
            }

            return new DeckDefinition(ids, historiaId);
        }
    }
}
