using System.Collections.Generic;
using System.Linq;
using System.Text;
using Game.Core.Effects;
using Game.Core.Model;
using Game.Runtime;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// v0.01 — Auditoría de efectos: cruza el CATÁLOGO (textos de efecto por carta) contra el
    /// EffectRegistry (handlers implementados) y reporta las cartas cuyo efecto NO se ejecutaría
    /// al jugarse. Es el fallo más silencioso del juego: la carta se juega, no pasa nada y no hay error.
    /// </summary>
    public static class EffectAudit
    {
        /// <summary>Triggers que NO viven en el registro: el motor los aplica directamente
        /// (pasivos continuos, penalizaciones dentro del propio handler, victoria del DÍA 7).</summary>
        private static readonly HashSet<EffectTrigger> EngineHandled = new()
        {
            EffectTrigger.EfectoPasivoContinuo, // ApplyEnterPassives / fases
            EffectTrigger.Penalizacion,         // se aplica dentro del handler del efecto principal
            EffectTrigger.EfectoDeArea,
            EffectTrigger.EfectoGlobal,
            EffectTrigger.EfectoDiferido,
            EffectTrigger.AlFinalDeLaEntrega,   // Victoria III, en el motor
        };

        [MenuItem("The Great Book/v0.01 Efectos/Auditar cobertura de efectos")]
        public static void Audit()
        {
            var catalog = UnityCatalogLoader.LoadDefault();
            if (catalog == null) { Debug.LogError("No se pudo cargar el catálogo (Resources/catalogo_v3)."); return; }
            var reg = CardEffects.BuildRegistry();

            var missing = new List<string>();
            var orphan = new List<string>();
            int ok = 0, motor = 0, sinEfecto = 0;

            foreach (var def in catalog.Cards.Values.OrderBy(d => d.Id))
            {
                foreach (var (trigger, texto) in ExpectedTriggers(def))
                {
                    if (string.IsNullOrWhiteSpace(texto) || IsNoOpText(texto)) { sinEfecto++; continue; }
                    if (EngineHandled.Contains(trigger)) { motor++; continue; }
                    if (reg.Has(def.Id, trigger)) ok++;
                    else missing.Add($"  {def.Id,-5} {def.Nombre,-32} [{trigger}]\n        \"{Trim(texto)}\"");
                }
            }

            // Handlers registrados para ids que el catálogo no conoce (typo en el id => efecto muerto).
            var known = new HashSet<string>(catalog.Cards.Values.Select(d => d.Id));
            foreach (var id in reg.CoveredCardIds())
                if (!known.Contains(id)) orphan.Add($"  {id} (handler registrado, id inexistente en el catálogo)");

            var sb = new StringBuilder();
            sb.AppendLine("=== AUDITORÍA DE EFECTOS (v0.01) ===");
            sb.AppendLine($"Cartas: {catalog.Cards.Count} · handlers registrados: {reg.Count}");
            sb.AppendLine($"Con handler: {ok} · resueltos por el motor: {motor} · sin texto: {sinEfecto} · SIN IMPLEMENTAR: {missing.Count}");
            if (missing.Count > 0)
            {
                sb.AppendLine().AppendLine($"--- {missing.Count} SIN HANDLER (la carta no haría nada al jugarse) ---");
                foreach (var m in missing) sb.AppendLine(m);
            }
            if (orphan.Count > 0)
            {
                sb.AppendLine().AppendLine($"--- {orphan.Count} HANDLERS HUÉRFANOS ---");
                foreach (var e in orphan) sb.AppendLine(e);
            }

            if (missing.Count == 0 && orphan.Count == 0) Debug.Log(sb.ToString());
            else Debug.LogWarning(sb.ToString());
        }

        /// <summary>Triggers que el catálogo IMPLICA para una carta, según sus campos y las
        /// ETIQUETAS del texto ("AL ENTRAR:", "AL SER DESTRUIDA:"…). Solo cuentan las etiquetas
        /// seguidas de ':' — así una mención de pasada ("copia el efecto AL ENTRAR del rival")
        /// no se confunde con un trigger propio.</summary>
        private static IEnumerable<(EffectTrigger trigger, string? texto)> ExpectedTriggers(CardDefinition d)
        {
            if (!string.IsNullOrWhiteSpace(d.AlEntrar)) yield return (EffectTrigger.AlEntrar, d.AlEntrar);
            if (!string.IsNullOrWhiteSpace(d.Activado)) yield return (EffectTrigger.EfectoActivado, d.Activado);
            if (!string.IsNullOrWhiteSpace(d.AlSalir)) yield return (EffectTrigger.AlSalir, d.AlSalir);

            if (string.IsNullOrWhiteSpace(d.Efecto)) yield break;

            var labelled = LabelledTriggers(d.Efecto!).ToList();
            if (labelled.Count > 0)
            {
                foreach (var t in labelled) yield return (t, d.Efecto);
                yield break;
            }

            // Sin etiquetas: el trigger por defecto depende del tipo de carta.
            switch (d.Type)
            {
                case CardType.Dia:
                    yield return (EffectTrigger.AlActivarElDia, d.Efecto); break;
                case CardType.Tierra:
                    if (d.Especial) yield return (EffectTrigger.AlTapearse, d.Efecto); // las básicas solo dan FD
                    break;
                case CardType.Concepto:
                    yield return (EsRespuesta(d) ? EffectTrigger.Respuesta : EffectTrigger.UsoUnico, d.Efecto); break;
                default:
                    yield return (EffectTrigger.AlEntrar, d.Efecto); break;
            }
        }

        private static readonly (string label, EffectTrigger trigger)[] Labels =
        {
            ("AL SER DESTRUIDA:", EffectTrigger.AlSerDestruida),
            ("AL ENTRAR:",        EffectTrigger.AlEntrar),
            ("AL SALIR:",         EffectTrigger.AlSalir),
            ("AL TAPEARSE:",      EffectTrigger.AlTapearse),
            ("PASIVO:",           EffectTrigger.EfectoPasivoContinuo),
            ("PENALIZACION:",     EffectTrigger.Penalizacion),
            ("PENALIZACIÓN:",     EffectTrigger.Penalizacion),
        };

        private static IEnumerable<EffectTrigger> LabelledTriggers(string texto)
        {
            var up = texto.ToUpperInvariant();
            return Labels.Where(l => up.Contains(l.label)).Select(l => l.trigger).Distinct();
        }

        private static bool EsRespuesta(CardDefinition d)
            => d.TipoConcepto != null && d.TipoConcepto.IndexOf("respuesta", System.StringComparison.OrdinalIgnoreCase) >= 0;

        /// <summary>Textos que declaran explícitamente que la carta no hace nada, o que apuntan
        /// a una condición de victoria resuelta por el motor.</summary>
        private static bool IsNoOpText(string t)
        {
            var s = t.Trim().ToLowerInvariant();
            return s.StartsWith("sin recompensa") || s.StartsWith("ninguno")
                || s.StartsWith("victoria_") || s == "-" || s == "—";
        }

        private static string Trim(string s)
        {
            s = s.Replace("\n", " ").Trim();
            return s.Length > 90 ? s.Substring(0, 87) + "..." : s;
        }
    }
}
