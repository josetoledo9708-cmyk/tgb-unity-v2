using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Data;
using Game.Core.Effects;
using Game.Core.Model;

namespace Game.Core.Engine
{
    /// <summary>
    /// Objeto central del Core: posee el estado, el catálogo y el RNG determinista.
    /// El setup vive aquí; los comandos y fases se añaden en milestones siguientes.
    /// </summary>
    public sealed partial class GameEngine
    {
        public GameState State { get; private set; } = null!;
        public CardCatalog Catalog { get; }
        public DeterministicRng Rng { get; private set; } = null!;

        /// <summary>Resolutor de efectos (NullEffectResolver hasta M4).</summary>
        public IEffectResolver Effects { get; set; } = new NullEffectResolver();
        /// <summary>Proveedor de decisiones (Auto por defecto).</summary>
        public IDecisionProvider Decisions { get; set; } = new AutoDecisionProvider();

        private int _nextInstanceId = 1;

        public const int OpeningHand = 7;
        public const int HandLimit = 7;

        public GameEngine(CardCatalog catalog) => Catalog = catalog;

        public CardInstance NewInstance(CardDefinition def, int owner)
            => new CardInstance(_nextInstanceId++, def, owner);

        /// <summary>
        /// Ventana de respuesta: antes de resolver un efecto "ofensivo", el rival puede activar
        /// una trampa boca abajo. Devuelve la trampa a activar (o null). La capa de presentación
        /// la asigna; si es null, no hay ventana (tests). Solo aplica a triggers ofensivos.
        /// </summary>
        public System.Func<int, CardInstance, EffectCategory, CardInstance?>? ResponseWindow;

        /// <summary>Aviso a la vista de que una carta se ha hecho PÚBLICA (p. ej. buscada del mazo a
        /// la mano): ambos jugadores deben verla. (jugadorQueLaTomó, carta, motivo).</summary>
        public System.Action<int, CardInstance, string>? CardRevealed;

        private static readonly EffectTrigger[] _respondable =
        {
            EffectTrigger.AlEntrar, EffectTrigger.EfectoActivado,
            EffectTrigger.UsoUnico, EffectTrigger.AlActivarElDia
        };

        /// <summary>Dispara un trigger de efecto sobre una carta.</summary>
        public void Fire(CardInstance card, EffectTrigger trigger)
        {
            // Solo se ofrece responder si hay un efecto REAL que anular (no cartas vanilla).
            if (ResponseWindow != null && System.Array.IndexOf(_respondable, trigger) >= 0
                && Effects.HasEffect(card.Def.Id, trigger))
            {
                int defender = 1 - card.OwnerId;
                var cat = ResponseRules.CategoryOf(card.Def.Id, trigger);
                var trap = ResponseWindow(defender, card, cat);
                if (trap != null) ActivateResponse(trap); // anula el próximo efecto del atacante
            }
            bool tieneHandler = Effects.HasEffect(card.Def.Id, trigger);
            Effects.Resolve(new EffectContext(this, card, trigger));
            // Costura de pruebas: sin suscriptores no hace nada (ver EffectLog).
            EffectLog.Raise(card, trigger, tieneHandler, card.OwnerId);
        }

        public void StartGame(DeckDefinition deck0, DeckDefinition deck1,
                              ulong seed, int firstPlayer = 0)
        {
            ValidateOrThrow(deck0, 0);
            ValidateOrThrow(deck1, 1);

            State = new GameState { Seed = seed, FirstPlayer = firstPlayer, ActivePlayer = firstPlayer };
            Rng = new DeterministicRng(seed);

            SetupPlayer(State.Players[0], deck0);
            SetupPlayer(State.Players[1], deck1);

            State.TurnNumber = 1;
            State.Emit($"Partida iniciada. Primer jugador: {firstPlayer}. Semilla: {seed}.");
            BeginTurn();
        }

        private void ValidateOrThrow(DeckDefinition deck, int player)
        {
            var res = DeckValidator.Validate(deck, Catalog);
            if (!res.Ok)
                throw new ArgumentException($"Mazo inválido (jugador {player}): {res}");
        }

        private void SetupPlayer(PlayerState p, DeckDefinition deck)
        {
            p.HistoriaId = deck.HistoriaId;

            // Mazo principal.
            var mazo = deck.MainCardIds.Select(id => NewInstance(Catalog.Get(id), p.Id)).ToList();
            Rng.Shuffle(mazo);
            foreach (var c in mazo) p.Mazo.Add(c);

            // HISTORIA (carta sintética, externa, boca arriba horizontal).
            var hDef = Catalog.FindHistoria(deck.HistoriaId)!;
            var hCard = NewInstance(
                new CardDefinition(hDef.Id, hDef.Nombre, CardType.Historia), p.Id);
            p.Historia = hCard;

            // Pila DIA externa: dia1..dia7 en orden.
            for (int n = 1; n <= 7; n++)
                p.PilaDia.Add(NewInstance(Catalog.Get($"dia{n}"), p.Id));
            p.DiaActual = 1;

            // Mano de apertura.
            for (int i = 0; i < OpeningHand; i++)
                if (p.Mazo.Count > 0)
                    p.Mano.Add(p.Mazo.DrawTop());
        }
    }
}
