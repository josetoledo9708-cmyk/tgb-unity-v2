using System.Collections.Generic;

namespace Game.Core.Data
{
    /// <summary>
    /// Mazo declarado por un jugador: lista de ids de cartas principales (40-50) +
    /// la HISTORIA elegida. Las 7 DIA son fijas (dia1..dia7) y se añaden en el setup.
    /// </summary>
    public sealed class DeckDefinition
    {
        public IReadOnlyList<string> MainCardIds { get; }
        public string HistoriaId { get; }

        public DeckDefinition(IReadOnlyList<string> mainCardIds, string historiaId)
        {
            MainCardIds = mainCardIds;
            HistoriaId = historiaId;
        }
    }
}
