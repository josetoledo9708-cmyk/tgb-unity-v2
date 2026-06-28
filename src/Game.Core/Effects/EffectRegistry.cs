using System;
using System.Collections.Generic;
using Game.Core.Model;

namespace Game.Core.Effects
{
    /// <summary>
    /// Mapa (id de carta, trigger) -> handler. Cada carta registra solo los triggers que usa.
    /// Las cartas sin handler simplemente no hacen nada al dispararse.
    /// </summary>
    public sealed class EffectRegistry
    {
        private readonly Dictionary<(string, EffectTrigger), Action<EffectContext>> _map = new();

        public void On(string cardId, EffectTrigger trigger, Action<EffectContext> handler)
            => _map[(cardId, trigger)] = handler;

        public bool TryGet(string cardId, EffectTrigger trigger, out Action<EffectContext> handler)
            => _map.TryGetValue((cardId, trigger), out handler!);

        public int Count => _map.Count;
        public bool Has(string cardId, EffectTrigger trigger) => _map.ContainsKey((cardId, trigger));
    }

    public sealed class RegistryEffectResolver : IEffectResolver
    {
        private readonly EffectRegistry _registry;
        public RegistryEffectResolver(EffectRegistry registry) => _registry = registry;

        public void Resolve(EffectContext ctx)
        {
            if (_registry.TryGet(ctx.Self.Def.Id, ctx.Trigger, out var handler))
                handler(ctx);
        }
    }
}
