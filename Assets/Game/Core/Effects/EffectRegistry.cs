using System;
using System.Collections.Generic;
using System.Linq;
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

        /// <summary>Ids de carta distintos que tienen al menos un handler.</summary>
        public IReadOnlyCollection<string> CoveredCardIds()
            => _map.Keys.Select(k => k.Item1).Distinct().ToList();
    }

    public sealed class RegistryEffectResolver : IEffectResolver
    {
        private readonly EffectRegistry _registry;
        public RegistryEffectResolver(EffectRegistry registry) => _registry = registry;

        public bool IsResponse(string cardId) => _registry.Has(cardId, EffectTrigger.Respuesta);

        public void Resolve(EffectContext ctx)
        {
            if (!_registry.TryGet(ctx.Self.Def.Id, ctx.Trigger, out var handler))
                return;

            var owner = ctx.Owner;

            // Querubines: los efectos del jugador bloqueado no se disparan.
            if (owner.EffectsBlockedTurns > 0)
            {
                ctx.State.Emit($"Efecto de {ctx.Self.Nombre} bloqueado (efectos suspendidos)");
                return;
            }
            // Confusión de Lenguas: efectos activados deshabilitados globalmente.
            if (ctx.Trigger == EffectTrigger.EfectoActivado && ctx.State.ActivatedEffectsDisabled)
            {
                ctx.State.Emit($"Efecto activado de {ctx.Self.Nombre} deshabilitado (Confusión)");
                return;
            }
            // RESPUESTA rival: anula el próximo efecto de este jugador.
            if (owner.NegatedNextEffect && ctx.Trigger != EffectTrigger.Respuesta)
            {
                owner.NegatedNextEffect = false;
                ctx.State.Emit($"Efecto de {ctx.Self.Nombre} ANULADO por respuesta rival");
                return;
            }

            handler(ctx);
        }
    }
}
