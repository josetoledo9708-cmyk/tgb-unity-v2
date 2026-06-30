using System.Collections.Generic;
using System.Linq;
using Game.Core.Model;

namespace Game.Core.Effects
{
    /// <summary>
    /// Abstrae las decisiones de un jugador (targeting, sí/no, opciones). En hotseat lo
    /// implementa la consola/UI; en tests, un proveedor automático. Mantiene el Core
    /// determinista: el motor pide decisiones, no las inventa.
    /// </summary>
    public interface IDecisionProvider
    {
        CardInstance? ChooseCard(GameState s, IReadOnlyList<CardInstance> options,
                                 string prompt, bool optional);
        bool ChooseYesNo(GameState s, string prompt);
        int ChooseOption(GameState s, IReadOnlyList<string> options, string prompt);

        /// <summary>Devuelve las cartas en el orden elegido (1 = primera/arriba del mazo).</summary>
        IReadOnlyList<CardInstance> ChooseOrder(GameState s, IReadOnlyList<CardInstance> cards, string prompt);
    }

    /// <summary>
    /// Elige automáticamente. Si se le pasa el catálogo, prefiere una PIEZA de la historia del
    /// jugador activo que aún no esté en campo (para que los tutores de la IA caven hacia su
    /// condición de victoria). Si no, elige la primera opción legal. Para tests y la IA.
    /// </summary>
    public sealed class AutoDecisionProvider : IDecisionProvider
    {
        private readonly CardCatalog? _cat;
        public AutoDecisionProvider(CardCatalog? cat = null) => _cat = cat;

        public CardInstance? ChooseCard(GameState s, IReadOnlyList<CardInstance> options,
                                        string prompt, bool optional)
        {
            if (options.Count == 0) return null;
            var h = _cat?.FindHistoria(s.Active.HistoriaId);
            if (h != null)
            {
                var p = s.Active;
                bool InField(string n) =>
                    p.Seres.Cards.Any(c => c.Nombre == n) || p.Tierras.Cards.Any(c => c.Nombre == n);
                var pick = options.FirstOrDefault(o => h.Piezas.Contains(o.Nombre) && !InField(o.Nombre))
                           ?? options.FirstOrDefault(o => h.Piezas.Contains(o.Nombre));
                if (pick != null) return pick;
            }
            return options[0];
        }

        public bool ChooseYesNo(GameState s, string prompt) => true;

        public int ChooseOption(GameState s, IReadOnlyList<string> options, string prompt) => 0;

        public IReadOnlyList<CardInstance> ChooseOrder(GameState s, IReadOnlyList<CardInstance> cards, string prompt)
            => cards; // sin reordenar
    }
}
