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
        public bool HasEffect(string cardId, EffectTrigger trigger) => _registry.Has(cardId, trigger);

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

            // Regla general: tras resolver un efecto el mazo se baraja, para que nadie conserve
            // información del orden. Se exceptúan los efectos cuya GRACIA es dejar un orden elegido
            // (mirar el tope y recolocar): barajar ahí anularía el propio efecto.
            if (!KeepsDeckOrder.Contains(ctx.Self.Def.Id))
                EffectApi.ShuffleDeck(ctx.Engine, owner);
        }

        /// <summary>Cartas que ORDENAN el tope del mazo: su efecto se perdería si se barajara después.</summary>
        private static readonly HashSet<string> KeepsDeckOrder = new()
        {
            "t10",   // Betel
            "sh01",  // Adán
            "c16",   // El Sueño de la Escalera
            "c30",   // El Sueño de los Haces
            "c41",   // Los Sueños de José
            "dia4",  // mira el tope
        };
    }
}
