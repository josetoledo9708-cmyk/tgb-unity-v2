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
            var candidates = p.Mazo.Cards.Where(c => pred(c.Def)).ToList();
            if (candidates.Count == 0) return false;
            var card = eng.Decisions.ChooseCard(eng.State, candidates, "Busca una carta en el mazo", optional: true);
            eng.Rng.Shuffle(p.Mazo.Cards); // el mazo se revuelve tras buscar
            if (card == null) return false;
            p.Mazo.Remove(card);
            p.Mano.Add(card);
            eng.State.Emit($"P{p.Id} busca a mano ({card.Nombre})");
            return true;
        }

        public static bool SearchTierraToField(GameEngine eng, PlayerState p,
                                               Func<CardDefinition, bool>? pred = null)
        {
            if (p.Tierras.IsFull) return false;
            var candidates = p.Mazo.Cards
                .Where(c => c.Type == CardType.Tierra && (pred == null || pred(c.Def)))
                .ToList();
            if (candidates.Count == 0) return false;
            var card = eng.Decisions.ChooseCard(eng.State, candidates, "Elige una TIERRA para poner en campo", optional: true);
            eng.Rng.Shuffle(p.Mazo.Cards);
            if (card == null) return false;
            p.Mazo.Remove(card);
            card.Tapped = false;
            card.TurnsLeftRemaining = card.Def.TurnsLeft ?? 0;
            p.Tierras.Add(card);
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

        /// <summary>Muestra una carta al jugador (mirar/espiar). No cambia el estado.</summary>
        public static void Reveal(GameEngine eng, CardInstance? card, string prompt)
        {
            if (card == null) return;
            eng.Decisions.ChooseCard(eng.State, new[] { card }, prompt, optional: true);
            eng.State.Emit($"Revelado: {card.Nombre}");
        }

        /// <summary>Muestra varias cartas al jugador (revelar mano/zona). No cambia el estado.</summary>
        public static void RevealMany(GameEngine eng, IReadOnlyList<CardInstance> cards, string prompt)
        {
            if (cards.Count == 0) { eng.State.Emit($"{prompt}: vacía"); return; }
            eng.Decisions.ChooseCard(eng.State, cards.ToList(), prompt, optional: true);
            eng.State.Emit($"Reveladas {cards.Count} carta(s)");
        }

        // --- destrucción / control (familias 4-6) ---

        public static void DestroyAllSeresBothSides(GameEngine eng)
        {
            foreach (var p in eng.State.Players)
                foreach (var ser in p.Seres.Cards.ToList())
                    eng.SendToRetirados(p, p.Seres, ser, fireAlSalir: true);
            eng.State.Emit("El Diluvio: todos los SER a Retirados");
        }

        public static bool DestroyTierraByEffect(GameEngine eng, CardInstance tierra)
        {
            var owner = eng.State.Players[tierra.OwnerId];
            if (!owner.Tierras.Cards.Contains(tierra)) return false;
            if (tierra.Indestructible || owner.TierraProtected || eng.LotInField(owner))
            {
                eng.State.Emit($"Destrucción de {tierra.Nombre} bloqueada (protección)");
                return false;
            }
            eng.DestroyTierra(owner, tierra); // dispara AL_SER_DESTRUIDA
            return true;
        }

        public static bool DestroySer(GameEngine eng, CardInstance ser)
        {
            var owner = eng.State.Players[ser.OwnerId];
            if (!owner.Seres.Cards.Contains(ser)) return false;
            if (ser.Indestructible || ser.ProtectedUntilTurn >= eng.State.TurnNumber)
            {
                eng.State.Emit($"Destrucción de {ser.Nombre} bloqueada (protección)");
                return false;
            }
            eng.SendToRetirados(owner, owner.Seres, ser, fireAlSalir: true);
            return true;
        }

        public static int DestroyRivalSeresCosteMax(GameEngine eng, int byPlayerId, int maxCoste, int n)
        {
            var opp = eng.State.Players[1 - byPlayerId];
            var pool = opp.Seres.Cards.Where(s => (s.Def.Coste ?? 0) <= maxCoste).ToList();
            int d = 0;
            for (int i = 0; i < n && pool.Count > 0; i++)
            {
                var target = eng.Decisions.ChooseCard(eng.State, pool, "Destruir SER rival", false) ?? pool[0];
                pool.Remove(target);
                if (DestroySer(eng, target)) d++;
            }
            return d;
        }

        public static int BounceSeresToHand(GameEngine eng, PlayerState target, int n)
        {
            int b = 0;
            for (int i = 0; i < n && target.Seres.Count > 0; i++)
            {
                var ser = eng.Decisions.ChooseCard(eng.State, target.Seres.Cards.ToList(),
                    "Regresar SER a la mano", false) ?? target.Seres.Cards[0];
                target.Seres.Remove(ser);
                ser.Tapped = false; ser.DurLeft = ser.Def.Dur ?? 0;
                target.Mano.Add(ser);
                b++;
            }
            if (b > 0) eng.State.Emit($"P{target.Id} regresa {b} SER a la mano");
            return b;
        }

        public static bool SearchSerToHand(GameEngine eng, PlayerState p, Func<CardDefinition, bool>? extra = null)
            => SearchToHand(eng, p, d => CardTypeNames.IsSer(d.Type) && (extra == null || extra(d)));

        /// <summary>Mira las top n del mazo, lleva 1 (que cumpla pred, o la 1ª) a la mano, resto al fondo.</summary>
        public static bool LookTopTakeOne(GameEngine eng, PlayerState p, int n, Func<CardDefinition, bool>? pred = null)
        {
            var top = p.Mazo.Cards.Take(n).ToList();
            if (top.Count == 0) return false;
            var pick = top.FirstOrDefault(c => pred == null || pred(c.Def)) ?? (pred == null ? top[0] : null);
            if (pick != null)
            {
                p.Mazo.Remove(pick);
                p.Mano.Add(pick);
                eng.State.Emit($"P{p.Id} toma del tope ({pick.Nombre})");
            }
            // El resto mirado se queda; (simplificación: no se reordena al fondo).
            return pick != null;
        }

        /// <summary>Mira las top n del mazo y las reordena según la decisión (1 = arriba).</summary>
        public static void LookTopReorder(GameEngine eng, PlayerState p, int n)
        {
            var top = p.Mazo.Cards.Take(n).ToList();
            if (top.Count == 0) return;
            var ordered = eng.Decisions.ChooseOrder(eng.State, top,
                $"Reordena las {top.Count} cartas superiores (1 = arriba del mazo)");
            for (int i = 0; i < top.Count; i++) p.Mazo.Cards.RemoveAt(0);
            for (int i = ordered.Count - 1; i >= 0; i--) p.Mazo.Cards.Insert(0, ordered[i]);
            eng.State.Emit($"P{p.Id} reordena las {top.Count} superiores");
        }

        public static void ProtectAllSeres(GameEngine eng, PlayerState p, int turns)
        {
            foreach (var s in p.Seres.Cards) s.ProtectedUntilTurn = eng.State.TurnNumber + turns;
            eng.State.Emit($"P{p.Id} protege sus SER ({turns} turno/s)");
        }

        public static void ProtectOneSer(GameEngine eng, PlayerState p, int turns)
        {
            var ser = eng.Decisions.ChooseCard(eng.State, p.Seres.Cards.ToList(), "Proteger SER", true);
            if (ser != null) ser.ProtectedUntilTurn = eng.State.TurnNumber + turns;
        }

        public static bool SacrificeOwnSer(GameEngine eng, PlayerState p)
        {
            if (p.Seres.Count == 0) return false;
            var ser = eng.Decisions.ChooseCard(eng.State, p.Seres.Cards.ToList(), "Sacrificar SER", false)
                      ?? p.Seres.Cards[0];
            eng.SendToRetirados(p, p.Seres, ser, fireAlSalir: true);
            eng.State.Emit($"P{p.Id} sacrifica ({ser.Nombre})");
            return true;
        }

        public static int MillOpponentDeck(GameEngine eng, PlayerState opp, int n)
        {
            int m = 0;
            for (int i = 0; i < n && opp.Mazo.Count > 0; i++)
            {
                opp.Retirados.Add(opp.Mazo.DrawTop());
                m++;
            }
            return m;
        }

        public static bool HasTierraInField(PlayerState p, string nombre)
            => p.Tierras.Cards.Any(c => c.Nombre == nombre);

        public static bool OncePerGame(PlayerState p, string key)
        {
            if (p.OncePerGameUsed.Contains(key)) return false;
            p.OncePerGameUsed.Add(key);
            return true;
        }

        /// <summary>Destruye hasta n TIERRAs eligiendo del rival primero y luego propias.</summary>
        public static int DestroyTierras(GameEngine eng, int byPlayerId, int n)
        {
            var me = eng.State.Players[byPlayerId];
            var opp = eng.State.Players[1 - byPlayerId];
            var pool = opp.Tierras.Cards.Concat(me.Tierras.Cards).ToList();
            int destroyed = 0;
            for (int i = 0; i < n && pool.Count > 0; i++)
            {
                var target = eng.Decisions.ChooseCard(eng.State, pool, "Destruir TIERRA", optional: false)
                             ?? pool[0];
                pool.Remove(target);
                if (DestroyTierraByEffect(eng, target)) destroyed++;
            }
            return destroyed;
        }
    }
}
