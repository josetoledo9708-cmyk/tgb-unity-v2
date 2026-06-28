using System.Collections.Generic;
using System.Linq;
using Game.Core.Effects;
using Game.Core.Model;

namespace Game.Core.Engine
{
    public sealed partial class GameEngine
    {
        /// <summary>Comienza el turno del jugador activo: Preludio -> Genesis -> (Preparacion).</summary>
        public void BeginTurn()
        {
            if (State.IsOver) return;
            State.Phase = Phase.Preludio;
            PreludioStep(State.Active);
            if (State.IsOver) return;

            State.Phase = Phase.Genesis;
            GenesisStep(State.Active);
            if (State.IsOver) return;

            State.Phase = Phase.Preparacion;
        }

        /// <summary>Cierra la fase de Preparación: Entrega + pasar el turno al rival.</summary>
        public CommandResult EndTurn(IReadOnlyList<CardInstance>? discards = null)
        {
            if (State.IsOver) return CommandResult.Fail("La partida terminó.");
            if (State.Phase != Phase.Preparacion)
                return CommandResult.Fail("Solo se puede terminar el turno en Preparación.");

            State.Phase = Phase.Entrega;
            var res = EntregaStep(State.Active, discards);
            if (!res.Ok) { State.Phase = Phase.Preparacion; return res; }
            if (State.IsOver) return CommandResult.Success;

            State.ActivePlayer = 1 - State.ActivePlayer;
            State.TurnNumber++;
            BeginTurn();
            return CommandResult.Success;
        }

        private void PreludioStep(PlayerState p)
        {
            p.Fd = 0;
            foreach (var t in p.Tierras.Cards) t.Tapped = false;
            p.TierraPlayedThisTurn = false;
            p.SeresActivatedThisTurn.Clear();
            p.DiaFreeUsed = p.SacrificioUsed = p.DiluvioUsed = p.TierraProtected = false;

            // SER pierden 1 turno de duración; los que llegan a 0 abandonan el campo.
            foreach (var ser in p.Seres.Cards.ToList())
            {
                ser.DurLeft--;
                if (ser.DurLeft > 0) continue;

                if (ser.Nombre == "Benjamín")
                {
                    // Caso especial: regresa al mazo en vez de a Retirados.
                    p.Seres.Remove(ser);
                    ser.Tapped = false;
                    p.Mazo.Add(ser);
                    State.Emit("Benjamín regresa al mazo");
                    if (p.Seres.Cards.Any(c => c.Nombre == "José") && p.Mazo.Count > 0)
                    {
                        p.Mano.Add(p.Mazo.DrawTop());
                        State.Emit("José: roba al regresar Benjamín");
                    }
                }
                else
                {
                    State.Emit($"{ser.Nombre} agota su duración -> Retirados");
                    SendToRetirados(p, p.Seres, ser, fireAlSalir: true);
                }
            }

            // TIERRA con autodestrucción (Sodoma/Gomorra), salvo Lot en campo.
            foreach (var tierra in p.Tierras.Cards.ToList())
            {
                if (tierra.TurnsLeftRemaining <= 0) continue;
                tierra.TurnsLeftRemaining--;
                if (tierra.TurnsLeftRemaining <= 0 && !LotInField(p))
                {
                    State.Emit($"{tierra.Nombre} se autodestruye");
                    DestroyTierra(p, tierra);
                }
            }

            State.Emit("Preludio: FD=0, tierras destapeadas, flags reset");
        }

        private void GenesisStep(PlayerState p)
        {
            if (State.TurnNumber == 1)
            {
                State.Emit("Genesis: el primer jugador no roba en el turno 1");
                return;
            }
            if (p.Mazo.Count == 0)
            {
                State.Emit($"{p.Id} no puede robar: mazo vacío");
                State.DeclareWinner(1 - p.Id, VictoryId.II);
                return;
            }
            var c = p.Mazo.DrawTop();
            p.Mano.Add(c);
            State.Emit($"Genesis: roba ({c.Nombre})");
        }

        private CommandResult EntregaStep(PlayerState p, IReadOnlyList<CardInstance>? discards)
        {
            // Victoria III (field_at_entrega) se engancha en M3.
            CheckVictoryAtEntrega(p);
            if (State.IsOver) return CommandResult.Success;

            int excess = p.Mano.Count - HandLimit;
            if (excess > 0)
            {
                var toDiscard = ResolveDiscards(p, excess, discards);
                if (toDiscard == null)
                    return CommandResult.Fail($"Debes descartar {excess} carta(s) para terminar.");
                foreach (var c in toDiscard)
                {
                    p.Mano.Remove(c);
                    p.Retirados.Add(c);
                    State.Emit($"Entrega: descarta ({c.Nombre})");
                }
            }
            return CommandResult.Success;
        }

        private List<CardInstance>? ResolveDiscards(PlayerState p, int n,
                                                    IReadOnlyList<CardInstance>? provided)
        {
            if (provided != null)
            {
                if (provided.Count != n) return null;
                if (provided.Any(c => !p.Mano.Cards.Contains(c))) return null;
                return provided.ToList();
            }
            // Auto: pedir al proveedor de decisiones.
            var chosen = new List<CardInstance>();
            var pool = p.Mano.Cards.ToList();
            for (int i = 0; i < n && pool.Count > 0; i++)
            {
                var c = Decisions.ChooseCard(State, pool, "Descartar al límite de mano", optional: false)
                        ?? pool[0];
                chosen.Add(c);
                pool.Remove(c);
            }
            return chosen.Count == n ? chosen : null;
        }

        // --- helpers de zona ---

        public bool LotInField(PlayerState p) => p.Seres.Cards.Any(c => c.Nombre == "Lot");

        public void SendToRetirados(PlayerState p, Zone from, CardInstance card, bool fireAlSalir)
        {
            from.Remove(card);
            card.Tapped = false;
            card.FaceDown = false;
            p.Retirados.Add(card);
            if (fireAlSalir) Fire(card, EffectTrigger.AlSalir);
        }

        public void DestroyTierra(PlayerState p, CardInstance tierra)
        {
            p.Tierras.Remove(tierra);
            p.Retirados.Add(tierra);
            Fire(tierra, EffectTrigger.AlSerDestruida);
        }
    }
}
