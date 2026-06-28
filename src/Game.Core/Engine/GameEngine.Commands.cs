using System.Linq;
using Game.Core.Effects;
using Game.Core.Model;

namespace Game.Core.Engine
{
    public sealed partial class GameEngine
    {
        private CommandResult GuardPrep()
        {
            if (State.IsOver) return CommandResult.Fail("La partida terminó.");
            if (State.Phase != Phase.Preparacion)
                return CommandResult.Fail("Solo durante Preparación.");
            return CommandResult.Success;
        }

        public CommandResult PlayTierra(CardInstance card)
        {
            var g = GuardPrep(); if (!g.Ok) return g;
            var p = State.Active;
            if (!p.Mano.Cards.Contains(card)) return CommandResult.Fail("La carta no está en tu mano.");
            if (card.Type != CardType.Tierra) return CommandResult.Fail("No es una TIERRA.");
            if (p.TierraPlayedThisTurn) return CommandResult.Fail("Ya jugaste una TIERRA este turno.");
            if (p.Tierras.IsFull) return CommandResult.Fail("Máximo 7 TIERRAs en campo.");

            p.Mano.Remove(card);
            card.Tapped = false;
            card.TurnsLeftRemaining = card.Def.TurnsLeft ?? 0;
            p.Tierras.Add(card);
            p.TierraPlayedThisTurn = true;
            State.Emit($"juega TIERRA ({card.Nombre})");
            Fire(card, EffectTrigger.AlEntrar);
            return CommandResult.Success;
        }

        /// <summary>Juega una TIERRA ignorando el límite de 1/turno (efectos: Piedra de Jacob, etc.).</summary>
        public CommandResult PlayTierraFree(CardInstance card)
        {
            var p = State.Active;
            if (!p.Mano.Cards.Contains(card)) return CommandResult.Fail("La carta no está en tu mano.");
            if (card.Type != CardType.Tierra) return CommandResult.Fail("No es una TIERRA.");
            if (p.Tierras.IsFull) return CommandResult.Fail("Máximo 7 TIERRAs en campo.");
            p.Mano.Remove(card);
            card.Tapped = false;
            card.TurnsLeftRemaining = card.Def.TurnsLeft ?? 0;
            p.Tierras.Add(card);
            State.Emit($"juega TIERRA gratis ({card.Nombre})");
            Fire(card, EffectTrigger.AlEntrar);
            return CommandResult.Success;
        }

        public CommandResult TapTierra(CardInstance card)
        {
            var g = GuardPrep(); if (!g.Ok) return g;
            var p = State.Active;
            if (!p.Tierras.Cards.Contains(card)) return CommandResult.Fail("La TIERRA no está en tu campo.");
            if (card.Tapped) return CommandResult.Fail("Esa TIERRA ya está tapeada.");

            card.Tapped = true;
            int fd = card.Def.Fd ?? 1;
            p.Fd += fd;
            State.Emit($"tapea ({card.Nombre}) +{fd} FD (total {p.Fd})");
            Fire(card, EffectTrigger.AlTapearse);
            return CommandResult.Success;
        }

        public CommandResult PlaySer(CardInstance card)
        {
            var g = GuardPrep(); if (!g.Ok) return g;
            var p = State.Active;
            if (!p.Mano.Cards.Contains(card)) return CommandResult.Fail("La carta no está en tu mano.");
            if (!CardTypeNames.IsSer(card.Type)) return CommandResult.Fail("No es un SER.");
            if (p.Seres.IsFull) return CommandResult.Fail("Máximo 3 SER en campo.");
            int coste = card.Def.Coste ?? 0;
            if (p.Fd < coste) return CommandResult.Fail($"FD insuficiente ({p.Fd}/{coste}).");

            p.Fd -= coste;
            p.Mano.Remove(card);
            card.DurLeft = card.Def.Dur ?? 0;
            p.Seres.Add(card);
            State.Emit($"juega SER ({card.Nombre}) -{coste} FD");
            Fire(card, EffectTrigger.AlEntrar);
            return CommandResult.Success;
        }

        public CommandResult PlayConcepto(CardInstance card, bool faceDown)
        {
            var g = GuardPrep(); if (!g.Ok) return g;
            var p = State.Active;
            if (!p.Mano.Cards.Contains(card)) return CommandResult.Fail("La carta no está en tu mano.");
            if (card.Type != CardType.Concepto) return CommandResult.Fail("No es un CONCEPTO.");

            if (faceDown)
            {
                if (p.Concepto.IsFull) return CommandResult.Fail("Ya tienes una trampa boca abajo.");
                p.Mano.Remove(card);
                card.FaceDown = true;
                p.Concepto.Add(card);
                State.Emit($"coloca CONCEPTO boca abajo");
                return CommandResult.Success;
            }

            int coste = card.Def.Coste ?? 0;
            if (p.Fd < coste) return CommandResult.Fail($"FD insuficiente ({p.Fd}/{coste}).");
            p.Fd -= coste;
            p.Mano.Remove(card);
            State.Emit($"juega CONCEPTO ({card.Nombre}) -{coste} FD");
            Fire(card, EffectTrigger.UsoUnico);
            // Si el efecto no la reubicó (p.ej. victoria h5), va a Retirados.
            if (!p.Retirados.Cards.Contains(card) && !State.IsOver)
                p.Retirados.Add(card);
            return CommandResult.Success;
        }

        public CommandResult ActivateSerEffect(CardInstance card)
        {
            var g = GuardPrep(); if (!g.Ok) return g;
            var p = State.Active;
            if (!p.Seres.Cards.Contains(card)) return CommandResult.Fail("El SER no está en tu campo.");
            if (p.SeresActivatedThisTurn.Contains(card.InstanceId))
                return CommandResult.Fail("Ese SER ya activó su efecto este turno.");
            int cost = card.Def.ActCost ?? 0;
            if (p.Fd < cost) return CommandResult.Fail($"FD insuficiente ({p.Fd}/{cost}).");

            p.Fd -= cost;
            p.SeresActivatedThisTurn.Add(card.InstanceId);
            State.Emit($"activa efecto de ({card.Nombre}) -{cost} FD");
            Fire(card, EffectTrigger.EfectoActivado);
            return CommandResult.Success;
        }

        /// <summary>
        /// Activa el DÍA actual (en orden). useFree omite el coste FD usando la activación
        /// gratuita del turno (diaFreeUsed). La condición se exige igual (M3).
        /// </summary>
        public CommandResult ActivateDia(bool useFree = false)
        {
            var g = GuardPrep(); if (!g.Ok) return g;
            var p = State.Active;
            int diaNum = p.DiaActual;
            if (diaNum > 7) return CommandResult.Fail("No quedan DÍAs por activar.");

            var diaCard = p.PilaDia.Cards.FirstOrDefault(d => PlayerState.DiaNumero(d) == diaNum);
            if (diaCard == null) return CommandResult.Fail("No se encontró el DÍA actual.");

            if (!DiaConditionMet(p, diaNum))
                return CommandResult.Fail($"No se cumple la condición del día {diaNum}.");

            int coste = diaCard.Def.Coste ?? 0;
            if (useFree)
            {
                if (p.DiaFreeUsed) return CommandResult.Fail("Ya usaste la activación gratuita de DÍA.");
                p.DiaFreeUsed = true;
            }
            else
            {
                if (p.Fd < coste) return CommandResult.Fail($"FD insuficiente ({p.Fd}/{coste}).");
                p.Fd -= coste;
            }

            p.PilaDia.Remove(diaCard);
            p.Retirados.Add(diaCard);
            p.DiasActivados.Add(diaNum);
            p.DiaActual++;
            State.Emit($"activa DÍA {diaNum} ({diaCard.Nombre}){(useFree ? " [gratis]" : $" -{coste} FD")}");

            Fire(diaCard, EffectTrigger.AlActivarElDia); // recompensa (M4)
            OnDiaActivated(p, diaNum);                    // dia7 -> Victoria I (M3)
            return CommandResult.Success;
        }
    }
}
