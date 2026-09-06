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
            eng.CardRevealed?.Invoke(p.Id, card, "buscada en el mazo"); // pública: la ve el rival
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
                eng.CardRevealed?.Invoke(p.Id, pick, "tomada del tope del mazo");
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

        /// <summary>Mira las top n, deja tomar 1 que cumpla `pred` (opcional para el jugador) y
        /// devuelve el RESTO al tope del mazo en el orden que elija. Es el patrón de "Los Sueños de
        /// José": revelar varias, quedarse con una y recolocar las demás.</summary>
        public static bool LookTopTakeOneThenReorder(GameEngine eng, PlayerState p, int n,
                                                     Func<CardDefinition, bool>? pred = null)
        {
            var top = p.Mazo.Cards.Take(n).ToList();
            if (top.Count == 0) return false;
            foreach (var c in top) p.Mazo.Remove(c);
            RevealMany(eng, top, $"P{p.Id} revela las {top.Count} superiores");

            var elegibles = top.Where(c => pred == null || pred(c.Def)).ToList();
            CardInstance? pick = null;
            if (elegibles.Count > 0)
            {
                pick = eng.Decisions.ChooseCard(eng.State, elegibles, "Añade 1 carta a tu mano", optional: true);
                if (pick != null)
                {
                    top.Remove(pick);
                    p.Mano.Add(pick);
                    eng.State.Emit($"P{p.Id} añade a la mano ({pick.Nombre})");
                    eng.CardRevealed?.Invoke(p.Id, pick, "tomada del tope del mazo");
                }
            }

            // El resto vuelve AL TOPE en el orden que decida el jugador (1 = arriba).
            if (top.Count > 0)
            {
                var ordered = eng.Decisions.ChooseOrder(eng.State, top,
                    $"Devuelve {top.Count} carta(s) al tope del mazo (1 = arriba)");
                for (int i = ordered.Count - 1; i >= 0; i--) p.Mazo.Cards.Insert(0, ordered[i]);
                eng.State.Emit($"P{p.Id} devuelve {ordered.Count} carta(s) al tope");
            }
            return pick != null;
        }

        /// <summary>Canaán (t05). BENDECIDO: pone en campo 1 TIERRA "Hebrón" o "Salem" desde la MANO.
        /// Si no tiene ninguna de las dos en mano, AMBOS jugadores la buscan en su mazo y la colocan
        /// directamente en campo TAPEADA. Una sola vez por partida (lo controla quien llama).</summary>
        public static bool CanaanPutHebronOrSalem(GameEngine eng, PlayerState p)
        {
            bool EsHebronOSalem(CardDefinition d) => d.Id == "t06" || d.Id == "t07";

            // 1) Desde la mano (sin tapear: entra como una TIERRA jugada normal).
            var enMano = p.Mano.Cards.Where(c => EsHebronOSalem(c.Def)).ToList();
            if (enMano.Count > 0 && !p.Tierras.IsFull)
            {
                var pick = eng.Decisions.ChooseCard(eng.State, enMano, "Pon en campo Hebrón o Salem", optional: true);
                if (pick != null)
                {
                    p.Mano.Remove(pick);
                    pick.Tapped = false;
                    pick.TurnsLeftRemaining = pick.Def.TurnsLeft ?? 0;
                    p.Tierras.Add(pick);
                    eng.State.Emit($"P{p.Id} pone TIERRA desde la mano ({pick.Nombre})");
                    eng.Fire(pick, EffectTrigger.AlEntrar);
                    return true;
                }
            }

            // 2) Sin ninguna en mano: AMBOS jugadores la buscan en su mazo y la ponen TAPEADA.
            bool any = false;
            foreach (var jugador in eng.State.Players)
                if (SearchTierraToFieldTapped(eng, jugador, EsHebronOSalem)) any = true;
            return any;
        }

        /// <summary>Busca una TIERRA en el mazo y la coloca en campo YA TAPEADA (no genera FD este turno).</summary>
        public static bool SearchTierraToFieldTapped(GameEngine eng, PlayerState p,
                                                     Func<CardDefinition, bool> pred)
        {
            if (p.Tierras.IsFull) return false;
            var candidates = p.Mazo.Cards.Where(c => c.Type == CardType.Tierra && pred(c.Def)).ToList();
            if (candidates.Count == 0) return false;
            var card = eng.Decisions.ChooseCard(eng.State, candidates, "Elige la TIERRA a colocar (tapeada)", optional: false)
                       ?? candidates[0];
            p.Mazo.Remove(card);
            ShuffleDeck(eng, p);
            card.Tapped = true;   // entra tapeada: no produce FD hasta tu próximo Preludio
            card.TurnsLeftRemaining = card.Def.TurnsLeft ?? 0;
            p.Tierras.Add(card);
            eng.State.Emit($"P{p.Id} coloca TIERRA tapeada desde el mazo ({card.Nombre})");
            eng.Fire(card, EffectTrigger.AlEntrar);
            return true;
        }

        /// <summary>Mira las top n, se queda 1 en la MANO y manda EL RESTO a Retirados
        /// (El Fruto Prohibido). El jugador ve las n antes de elegir.</summary>
        public static bool LookTopTakeOneRestToRetirados(GameEngine eng, PlayerState p, int n,
                                                         Func<CardDefinition, bool>? pred = null)
        {
            var top = p.Mazo.Cards.Take(n).ToList();
            if (top.Count == 0) return false;
            foreach (var c in top) p.Mazo.Remove(c);
            RevealMany(eng, top, $"P{p.Id} revela las {top.Count} superiores");

            var elegibles = top.Where(c => pred == null || pred(c.Def)).ToList();
            CardInstance? pick = null;
            if (elegibles.Count > 0)
            {
                pick = eng.Decisions.ChooseCard(eng.State, elegibles, "Añade 1 carta a tu mano", optional: false)
                       ?? elegibles[0];
                top.Remove(pick);
                p.Mano.Add(pick);
                eng.State.Emit($"P{p.Id} añade a la mano ({pick.Nombre})");
                eng.CardRevealed?.Invoke(p.Id, pick, "tomada del tope del mazo");
            }

            // El resto NO vuelve al mazo: se va a Retirados.
            foreach (var c in top) p.Retirados.Add(c);
            if (top.Count > 0) eng.State.Emit($"P{p.Id} envía {top.Count} carta(s) a Retirados");
            return pick != null;
        }

        /// <summary>Revela al jugador la carta del FONDO del mazo (no la mueve).</summary>
        public static bool RevealBottom(GameEngine eng, PlayerState p)
        {
            if (p.Mazo.Count == 0) return false;
            var bottom = p.Mazo.Cards[p.Mazo.Count - 1];
            Reveal(eng, bottom, $"P{p.Id} mira el fondo de su mazo");
            return true;
        }

        /// <summary>OPCIONAL: descartar 1 carta elegida de la mano para robar 1 (Río Tigris).</summary>
        public static bool OptionalDiscardToDraw(GameEngine eng, PlayerState p)
        {
            if (p.Mano.Count == 0) return false;
            if (!eng.Decisions.ChooseYesNo(eng.State, "¿Descartar 1 carta para robar 1?")) return false;
            var pick = eng.Decisions.ChooseCard(eng.State, p.Mano.Cards.ToList(), "Descarta 1 carta", optional: true);
            if (pick == null) return false;
            p.Mano.Remove(pick);
            p.Retirados.Add(pick);
            eng.State.Emit($"P{p.Id} descarta ({pick.Nombre}) para robar");
            Draw(eng, p, 1);
            return true;
        }

        /// <summary>Baraja el mazo del jugador (tras efectos de búsqueda, para no dejarlo ordenado).</summary>
        public static void ShuffleDeck(GameEngine eng, PlayerState p)
        {
            if (p.Mazo.Count <= 1) return;
            eng.Rng.Shuffle(p.Mazo.Cards);
            eng.State.Emit($"P{p.Id} baraja su mazo");
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
