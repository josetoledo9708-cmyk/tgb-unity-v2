using System;
using System.Collections.Generic;
using Game.Core.Model;

namespace Game.Core.Effects
{
    /// <summary>Categoría de un efecto ofensivo, para decidir qué trampa puede responderlo.</summary>
    [Flags]
    public enum EffectCategory
    {
        None = 0,
        DestruyeTierra = 1,
        EnviaSerRetirados = 2,
        ActivaDia = 4,
        RecuperaTierra = 8,
        Otro = 16
    }

    /// <summary>
    /// Reglas de RESPUESTA: categoría de cada efecto ofensivo y qué categorías cancela cada
    /// carta de respuesta. Permite que las trampas condicionales solo se ofrezcan cuando aplican.
    /// </summary>
    public static class ResponseRules
    {
        // Categoría del efecto atacante por (cardId, trigger). Lo no listado = Otro.
        private static readonly Dictionary<(string, EffectTrigger), EffectCategory> Cat = new()
        {
            { ("sd3", EffectTrigger.AlEntrar), EffectCategory.DestruyeTierra },        // Los Ángeles de Sodoma
            { ("sh03", EffectTrigger.AlEntrar), EffectCategory.DestruyeTierra },       // Caín
            { ("c11", EffectTrigger.UsoUnico), EffectCategory.DestruyeTierra },        // La Destrucción de Sodoma
            { ("c07", EffectTrigger.UsoUnico), EffectCategory.EnviaSerRetirados },     // El Diluvio
            { ("sh12b", EffectTrigger.EfectoActivado), EffectCategory.EnviaSerRetirados },
            { ("sh24", EffectTrigger.EfectoActivado), EffectCategory.EnviaSerRetirados },
            { ("c08", EffectTrigger.UsoUnico), EffectCategory.RecuperaTierra },        // La Rama de Olivo
            { ("c24", EffectTrigger.UsoUnico), EffectCategory.RecuperaTierra },        // La Promesa de la Tierra
            { ("c39", EffectTrigger.UsoUnico), EffectCategory.RecuperaTierra },        // El Viaje del Siervo
            { ("t05", EffectTrigger.AlEntrar), EffectCategory.RecuperaTierra },        // Canaán
        };

        // Qué categorías cancela cada carta de respuesta (lo no listado = cualquiera).
        private static readonly Dictionary<string, EffectCategory> Resp = new()
        {
            { "c19", (EffectCategory)(~0) },                                            // niega cualquier efecto
            { "c12", EffectCategory.DestruyeTierra | EffectCategory.EnviaSerRetirados },
            { "c21", EffectCategory.EnviaSerRetirados },
            { "c32", EffectCategory.DestruyeTierra },
            { "c03", EffectCategory.ActivaDia | EffectCategory.RecuperaTierra },
            { "c29", EffectCategory.ActivaDia },
        };

        /// <summary>Categoría del efecto atacante. Activar un DÍA es siempre ActivaDia.</summary>
        public static EffectCategory CategoryOf(string cardId, EffectTrigger trigger)
        {
            if (trigger == EffectTrigger.AlActivarElDia) return EffectCategory.ActivaDia;
            return Cat.TryGetValue((cardId, trigger), out var c) ? c : EffectCategory.Otro;
        }

        /// <summary>True si esa carta de respuesta puede cancelar un efecto de esa categoría.</summary>
        public static bool Applies(string trapCardId, EffectCategory category)
        {
            if (!Resp.TryGetValue(trapCardId, out var mask)) return true; // trampa desconocida: permitir
            return (mask & category) != 0;
        }
    }
}
