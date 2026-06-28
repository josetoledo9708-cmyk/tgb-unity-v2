using System.Collections.Generic;
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
    }

    /// <summary>Elige siempre la primera opción legal. Para tests y como fallback.</summary>
    public sealed class AutoDecisionProvider : IDecisionProvider
    {
        public CardInstance? ChooseCard(GameState s, IReadOnlyList<CardInstance> options,
                                        string prompt, bool optional)
            => options.Count > 0 ? options[0] : null;

        public bool ChooseYesNo(GameState s, string prompt) => true;

        public int ChooseOption(GameState s, IReadOnlyList<string> options, string prompt) => 0;
    }
}
