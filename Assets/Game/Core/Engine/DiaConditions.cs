using System.Linq;
using Game.Core.Model;

namespace Game.Core.Engine
{
    /// <summary>
    /// Evalúa la condición de cada DÍA (1-7) contra el estado del jugador.
    /// Refleja el texto del catálogo (campo "condicion").
    /// </summary>
    public static class DiaConditions
    {
        public static bool Met(PlayerState p, int dia) => dia switch
        {
            1 => Tierras(p) >= 1,
            2 => Tierras(p) >= 2,
            3 => Tierras(p) >= 3 && ConceptoEnManoORetirados(p),
            4 => Tierras(p) >= 3 && SeresConCosteMin(p, 3) >= 1,
            5 => Seres(p) >= 2 && TierrasSinTapear(p) >= 2,
            6 => Seres(p) >= 2 && Tierras(p) >= 4 && ConceptoDisponible(p),
            7 => Dias1a6Activados(p) && Tierras(p) >= 3
                 && SeresConCosteMax(p, 2) >= 1 && SeresConCosteMin(p, 3) >= 1,
            _ => false
        };

        private static int Tierras(PlayerState p) => p.Tierras.Count;
        private static int Seres(PlayerState p) => p.Seres.Count;
        private static int TierrasSinTapear(PlayerState p) => p.Tierras.Cards.Count(t => !t.Tapped);

        private static int SeresConCosteMin(PlayerState p, int min)
            => p.Seres.Cards.Count(s => (s.Def.Coste ?? 0) >= min);
        private static int SeresConCosteMax(PlayerState p, int max)
            => p.Seres.Cards.Count(s => (s.Def.Coste ?? 0) <= max);

        private static bool ConceptoEnManoORetirados(PlayerState p)
            => p.Mano.Cards.Any(c => c.Type == CardType.Concepto)
            || p.Retirados.Cards.Any(c => c.Type == CardType.Concepto);

        private static bool ConceptoDisponible(PlayerState p)
            => ConceptoEnManoORetirados(p) || p.Concepto.Count > 0;

        private static bool Dias1a6Activados(PlayerState p)
        {
            for (int n = 1; n <= 6; n++)
                if (!p.DiasActivados.Contains(n)) return false;
            return true;
        }
    }
}
