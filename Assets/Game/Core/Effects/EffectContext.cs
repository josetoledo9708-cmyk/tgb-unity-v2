using Game.Core.Engine;
using Game.Core.Model;

namespace Game.Core.Effects
{
    /// <summary>Contexto pasado a cada handler de efecto.</summary>
    public sealed class EffectContext
    {
        public GameEngine Engine { get; }
        public GameState State => Engine.State;
        public CardInstance Self { get; }
        public EffectTrigger Trigger { get; }

        /// <summary>Dueño de la carta (quien controla el efecto).</summary>
        public PlayerState Owner => State.Players[Self.OwnerId];
        public PlayerState Opponent => State.Players[1 - Self.OwnerId];
        public IDecisionProvider Decisions => Engine.Decisions;

        public EffectContext(GameEngine engine, CardInstance self, EffectTrigger trigger)
        {
            Engine = engine;
            Self = self;
            Trigger = trigger;
        }
    }

    public interface IEffectResolver
    {
        void Resolve(EffectContext ctx);
    }

    /// <summary>No hace nada (M2: estructura sin efectos). M4 lo reemplaza por el registry.</summary>
    public sealed class NullEffectResolver : IEffectResolver
    {
        public void Resolve(EffectContext ctx) { }
    }
}
