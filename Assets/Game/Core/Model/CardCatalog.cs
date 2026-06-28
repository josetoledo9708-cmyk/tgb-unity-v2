using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Model
{
    /// <summary>
    /// Catálogo completo en memoria: cartas jugables (DIA/TIERRA/SER/CONCEPTO) por id,
    /// más las definiciones de HISTORIA y la pila de DIA.
    /// </summary>
    public sealed class CardCatalog
    {
        private readonly Dictionary<string, CardDefinition> _byId;

        public IReadOnlyDictionary<string, CardDefinition> Cards => _byId;
        public IReadOnlyList<HistoriaDef> Historias { get; }
        public IReadOnlyList<DiaDef> Dias { get; }

        public CardCatalog(IEnumerable<CardDefinition> cards,
                           IReadOnlyList<HistoriaDef> historias,
                           IReadOnlyList<DiaDef> dias)
        {
            _byId = cards.ToDictionary(c => c.Id);
            Historias = historias;
            Dias = dias;
        }

        public CardDefinition Get(string id) => _byId[id];
        public bool TryGet(string id, out CardDefinition card) => _byId.TryGetValue(id, out card!);

        public int CountByType(CardType type) => _byId.Values.Count(c => c.Type == type);

        public IEnumerable<CardDefinition> OfType(CardType type)
            => _byId.Values.Where(c => c.Type == type);

        public HistoriaDef? FindHistoria(string id)
            => Historias.FirstOrDefault(h => h.Id == id);
    }
}
