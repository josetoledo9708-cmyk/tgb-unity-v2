using System.Collections.Generic;
using System.Linq;
using Game.Core.Model;

namespace Game.Core.Engine
{
    public sealed partial class GameEngine
    {
        private bool DiaConditionMet(PlayerState p, int diaNum) => DiaConditions.Met(p, diaNum);

        /// <summary>Victoria I: activar el día 7 (su condición ya se validó al activarlo).</summary>
        private void OnDiaActivated(PlayerState p, int diaNum)
        {
            if (diaNum == 7)
                State.DeclareWinner(p.Id, VictoryId.I);
        }

        /// <summary>Victoria III (field_at_entrega): 5 piezas en campo al fin de Entrega.</summary>
        private void CheckVictoryAtEntrega(PlayerState p)
        {
            var h = Catalog.FindHistoria(p.HistoriaId);
            if (h == null || h.ModoVictoria != VictoryMode.FieldAtEntrega) return;
            if (h.Piezas.All(pieza => PieceInField(p, pieza)))
                State.DeclareWinner(p.Id, VictoryId.III);
        }

        /// <summary>
        /// Victoria III (on_piece_play, h5): al jugar la 5ª pieza (un CONCEPTO) con las otras
        /// 4 piezas en campo, antes de resolver su efecto.
        /// </summary>
        public void CheckVictoryOnPiecePlay(PlayerState p, CardInstance justPlayed)
        {
            var h = Catalog.FindHistoria(p.HistoriaId);
            if (h == null || h.ModoVictoria != VictoryMode.OnPiecePlay) return;
            if (!h.Piezas.Contains(justPlayed.Nombre)) return;

            var others = h.Piezas.Where(x => x != justPlayed.Nombre);
            if (others.All(pieza => PieceInField(p, pieza)))
                State.DeclareWinner(p.Id, VictoryId.III);
        }

        /// <summary>Una pieza (por nombre) está "en campo" si está en Tierras o Seres del jugador.</summary>
        private static bool PieceInField(PlayerState p, string nombre)
            => p.Tierras.Cards.Any(c => c.Nombre == nombre)
            || p.Seres.Cards.Any(c => c.Nombre == nombre);
    }
}
