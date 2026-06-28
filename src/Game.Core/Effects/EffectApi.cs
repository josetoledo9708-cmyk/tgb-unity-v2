using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Engine;
using Game.Core.Model;

namespace Game.Core.Effects
{
    /// <summary>
    /// Primitivas reutilizables para construir los efectos de las cartas. Operan sobre el
    /// estado a través del GameEngine para mantener consistencia (eventos, zonas).
    /// </summary>
    public static class EffectApi
    {
        public static int Draw(GameEngine eng, PlayerState p, int n)
        {
            int drawn = 0;
            for (int i = 0; i < n && p.Mazo.Count > 0; i++)
            {
                p.Mano.Add(p.Mazo.DrawTop());
                drawn++;
            }
            if (drawn > 0) eng.State.Emit($"P{p.Id} roba {drawn} (efecto)");
            return drawn;
        }

        public static void AddFd(GameEngine eng, PlayerState p, int n)
        {
            p.Fd = Math.Max(0, p.Fd + n);
            eng.State.Emit($"P{p.Id} {(n >= 0 ? "+" : "")}{n} FD (efecto) -> {p.Fd}");
        }

        public static int DiscardRandom(GameEngine eng, PlayerState p, int n)
        {
            int d = 0;
            for (int i = 0; i < n && p.Mano.Count > 0; i++)
            {
                int idx = eng.Rng.NextInt(p.Mano.Count);
                var c = p.Mano.Cards[idx];
                p.Mano.Cards.RemoveAt(idx);
                p.Retirados.Add(c);
                d++;
            }
            if (d > 0) eng.State.Emit($"P{p.Id} descarta {d} al azar (efecto)");
            return d;
        }

        public static bool SearchToHand(GameEngine eng, PlayerState p,
                                        Func<CardDefinition, bool> pred)
        {
            var card = p.Mazo.Cards.FirstOrDefault(c => pred(c.Def));
            if (card == null) return false;
            p.Mazo.Remove(card);
            p.Mano.Add(card);
            eng.Rng.Shuffle(p.Mazo.Cards);
            eng.State.Emit($"P{p.Id} busca a mano ({card.Nombre})");
            return true;
        }

        public static bool SearchTierraToField(GameEngine eng, PlayerState p,
                                               Func<CardDefinition, bool>? pred = null)
        {
            var card = p.Mazo.Cards.FirstOrDefault(c =>
                c.Type == CardType.Tierra && (pred == null || pred(c.Def)));
            if (card == null || p.Tierras.IsFull) return false;
            p.Mazo.Remove(card);
            card.Tapped = false;
            card.TurnsLeftRemaining = card.Def.TurnsLeft ?? 0;
            p.Tierras.Add(card);
            eng.Rng.Shuffle(p.Mazo.Cards);
            eng.State.Emit($"P{p.Id} pone TIERRA en campo ({card.Nombre})");
            eng.Fire(card, EffectTrigger.AlEntrar);
            return true;
        }

        public static bool ReturnFromRetiradosToHand(GameEngine eng, PlayerState p,
                                                     Func<CardDefinition, bool> pred)
        {
            var card = p.Retirados.Cards.FirstOrDefault(c => pred(c.Def));
            if (card == null) return false;
            p.Retirados.Remove(card);
            p.Mano.Add(card);
            eng.State.Emit($"P{p.Id} recupera de Retirados ({card.Nombre})");
            return true;
        }

        public static bool ReturnSerFromRetiradosToField(GameEngine eng, PlayerState p)
        {
            var card = p.Retirados.Cards.FirstOrDefault(c => CardTypeNames.IsSer(c.Type));
            if (card == null || p.Seres.IsFull) return false;
            p.Retirados.Remove(card);
            card.DurLeft = card.Def.Dur ?? 0;
            p.Seres.Add(card);
            eng.State.Emit($"P{p.Id} revive SER ({card.Nombre})");
            eng.Fire(card, EffectTrigger.AlEntrar);
            return true;
        }

        public static void ModifyDurAll(GameEngine eng, PlayerState p, int n)
        {
            foreach (var s in p.Seres.Cards) s.DurLeft += n;
            eng.State.Emit($"P{p.Id} dur {(n >= 0 ? "+" : "")}{n} a todos sus SER");
        }

        public static bool HasSerInField(PlayerState p, string nombre)
            => p.Seres.Cards.Any(c => c.Nombre == nombre);
    }
}
