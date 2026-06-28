using System.Collections.Generic;
using System.Linq;

namespace Game.Core.Model
{
    public sealed class PlayerState
    {
        public int Id { get; }

        public Zone Mano { get; } = new(ZoneId.Mano);
        public Zone Mazo { get; } = new(ZoneId.Mazo);
        public Zone Tierras { get; } = new(ZoneId.Tierras, capacity: 7);
        public Zone Seres { get; } = new(ZoneId.Seres, capacity: 3);
        public Zone Concepto { get; } = new(ZoneId.Concepto, capacity: 1); // trampa boca abajo
        public Zone Retirados { get; } = new(ZoneId.Retirados);
        public Zone PilaDia { get; } = new(ZoneId.PilaDia); // dia1..dia7 en orden
        public CardInstance? Historia { get; set; }

        public int Fd { get; set; }
        public int DiaActual { get; set; } = 1; // próximo DIA a activar (1..7)
        public HashSet<int> DiasActivados { get; } = new();
        public string HistoriaId { get; set; } = "";

        // Flags de balance (se resetean en Preludio).
        public bool DiaFreeUsed { get; set; }
        public bool SacrificioUsed { get; set; }
        public bool DiluvioUsed { get; set; }
        public bool TierraProtected { get; set; }

        // Estados temporales (efectos de cartas).
        public int DiaBlockedTurns { get; set; }        // Babel: no puede activar DÍA.
        public int TierrasNoFdTurns { get; set; }       // Maldición de la Tierra: tus TIERRAs dan 0 FD.
        public bool ConceptosBlockedThisTurn { get; set; } // Ángel de la Torre.
        public int NextSerDurBonus { get; set; }        // Túnica de Colores: +dur al próximo SER.
        public int EffectsBlockedTurns { get; set; }    // Querubines: tus efectos no se disparan.
        public bool NegatedNextEffect { get; set; }     // RESPUESTA rival: anula tu próximo efecto.
        public HashSet<string> OncePerGameUsed { get; } = new(); // efectos "una vez por partida".

        // Control por turno.
        public bool TierraPlayedThisTurn { get; set; }
        public HashSet<int> SeresActivatedThisTurn { get; } = new(); // InstanceId

        public PlayerState(int id) => Id = id;

        public int FdDisponibleAlTapear()
            => Tierras.Cards.Where(t => !t.Tapped).Sum(t => t.Def.Fd ?? 1);

        public CardInstance? DiaActualCard()
            => PilaDia.Cards.FirstOrDefault(d => (d.Def.Coste.HasValue || true)
                && DiaNumero(d) == DiaActual);

        public static int DiaNumero(CardInstance dia)
        {
            // ids "dia1".."dia7"
            var id = dia.Def.Id;
            return int.TryParse(id.Replace("dia", ""), out var n) ? n : 0;
        }
    }
}
