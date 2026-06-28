using System;
using System.Collections.Generic;
using System.Linq;
using Game.Core.Data;
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

        private int _nextInstanceId = 1;

        public const int OpeningHand = 7;
        public const int HandLimit = 7;

        public GameEngine(CardCatalog catalog) => Catalog = catalog;

        public CardInstance NewInstance(CardDefinition def, int owner)
            => new CardInstance(_nextInstanceId++, def, owner);

        public void StartGame(DeckDefinition deck0, DeckDefinition deck1,
                              ulong seed, int firstPlayer = 0)
        {
            ValidateOrThrow(deck0, 0);
            ValidateOrThrow(deck1, 1);

            State = new GameState { Seed = seed, FirstPlayer = firstPlayer, ActivePlayer = firstPlayer };
            Rng = new DeterministicRng(seed);

            SetupPlayer(State.Players[0], deck0);
            SetupPlayer(State.Players[1], deck1);

            State.Phase = Phase.Preludio;
            State.TurnNumber = 1;
            State.Emit($"Partida iniciada. Primer jugador: {firstPlayer}. Semilla: {seed}.");
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
