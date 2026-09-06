using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using Game.Core.Data;
using Game.Core.Effects;
using Game.Core.Engine;
using Game.Core.Model;
using Game.Runtime;

namespace Game.Tests
{
    /// <summary>
    /// v0.01 — Verifica que los efectos de las cartas SE EJECUTEN al jugarse. La auditoría estática
    /// solo comprueba que exista un handler; esto lo dispara de verdad contra un motor real y exige
    /// que (a) no lance excepción y (b) deje rastro en el estado (log o cambio observable).
    /// </summary>
    public class EffectExecutionTests
    {
        private static CardCatalog LoadCatalog()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "catalogo.v3.json");
            return UnityCatalogLoader.FromJson(File.ReadAllText(path));
        }

        private static GameEngine NewEngine(CardCatalog cat)
        {
            var eng = new GameEngine(cat)
            {
                Effects = CardEffects.BuildResolver(),
                Decisions = new AutoDecisionProvider(cat),
            };
            eng.StartGame(
                SampleDeckBuilder.Build(cat, "h1", 40, pieceCopies: 3),
                SampleDeckBuilder.Build(cat, "h2", 40, pieceCopies: 3),
                seed: 4242, firstPlayer: 0);
            return eng;
        }

        /// <summary>Dispara TODOS los handlers del registro y falla si alguno revienta.</summary>
        [Test]
        public void Every_Registered_Effect_Runs_Without_Exception()
        {
            var cat = LoadCatalog();
            var registry = CardEffects.BuildRegistry();
            var resolver = new RegistryEffectResolver(registry);
            var triggers = (EffectTrigger[])Enum.GetValues(typeof(EffectTrigger));

            var failures = new List<string>();
            int fired = 0;

            foreach (var id in registry.CoveredCardIds().OrderBy(x => x))
            {
                if (!cat.TryGet(id, out var def)) continue;

                foreach (var trigger in triggers)
                {
                    if (!registry.Has(id, trigger)) continue;

                    // Motor limpio por efecto: un handler no debe depender del anterior.
                    var eng = NewEngine(cat);
                    eng.Effects = resolver;
                    var card = new CardInstance(900000 + fired, def, ownerId: 0);
                    PlaceForTrigger(eng, card, trigger);

                    try
                    {
                        resolver.Resolve(new EffectContext(eng, card, trigger));
                        fired++;
                    }
                    catch (Exception e)
                    {
                        failures.Add($"{id} \"{def.Nombre}\" [{trigger}] -> {e.GetType().Name}: {e.Message}");
                    }
                }
            }

            Debug.Log($"Efectos disparados sin error: {fired} · fallos: {failures.Count}");
            Assert.IsEmpty(failures, "Efectos que lanzan excepción al ejecutarse:\n" + string.Join("\n", failures));
        }

        /// <summary>Un efecto que no deja NINGÚN rastro (ni log ni cambio de estado) es sospechoso
        /// de ser un no-op silencioso. Se reporta como aviso, no como fallo: hay efectos
        /// legítimamente condicionales que no aplican en un campo vacío.</summary>
        [Test]
        public void Report_Effects_With_No_Observable_Change()
        {
            var cat = LoadCatalog();
            var registry = CardEffects.BuildRegistry();
            var resolver = new RegistryEffectResolver(registry);
            var triggers = (EffectTrigger[])Enum.GetValues(typeof(EffectTrigger));

            var silent = new List<string>();
            int total = 0;

            foreach (var id in registry.CoveredCardIds().OrderBy(x => x))
            {
                if (!cat.TryGet(id, out var def)) continue;

                foreach (var trigger in triggers)
                {
                    if (!registry.Has(id, trigger)) continue;
                    total++;

                    var eng = NewEngine(cat);
                    eng.Effects = resolver;
                    var card = new CardInstance(900000 + total, def, ownerId: 0);
                    PlaceForTrigger(eng, card, trigger);

                    var before = Snapshot(eng.State);
                    int logBefore = eng.State.Log.Count;
                    try { resolver.Resolve(new EffectContext(eng, card, trigger)); }
                    catch { continue; } // los fallos los cubre el test anterior
                    if (eng.State.Log.Count == logBefore && Snapshot(eng.State) == before)
                        silent.Add($"  {id,-5} {def.Nombre,-30} [{trigger}]");
                }
            }

            var sb = new StringBuilder();
            sb.AppendLine($"=== EFECTOS SIN CAMBIO OBSERVABLE ({silent.Count}/{total}) ===");
            sb.AppendLine("(revisar a mano: pueden ser condicionales que no aplican con el campo vacío)");
            foreach (var s in silent) sb.AppendLine(s);
            Debug.Log(sb.ToString());

            Assert.Pass($"{silent.Count} de {total} efectos sin cambio observable; ver consola.");
        }

        /// <summary>Coloca la carta donde el trigger espera encontrarla, para que el handler
        /// opere sobre un campo coherente (si no, muchos efectos leen zonas vacías).</summary>
        private static void PlaceForTrigger(GameEngine eng, CardInstance card, EffectTrigger trigger)
        {
            var p = eng.State.Players[0];
            switch (card.Def.Type)
            {
                case CardType.Tierra:
                    if (!p.Tierras.IsFull) p.Tierras.Add(card);
                    break;
                case CardType.Concepto:
                    if (!p.Concepto.IsFull) p.Concepto.Add(card);
                    break;
                case CardType.Dia:
                    break; // los DÍA viven en su propia pila, ya montada por StartGame
                default:
                    if (CardTypeNames.IsSer(card.Type) && !p.Seres.IsFull)
                    {
                        card.DurLeft = card.Def.Dur ?? 0;
                        p.Seres.Add(card);
                    }
                    break;
            }
            p.Fd = 10; // FD holgado: los efectos que cobran no deben fallar por falta de recursos
        }

        /// <summary>Huella del estado observable: si no cambia, el efecto no hizo nada.</summary>
        private static string Snapshot(GameState s)
        {
            var sb = new StringBuilder();
            foreach (var p in s.Players)
                sb.Append(p.Fd).Append('|').Append(p.Mano.Count).Append('|').Append(p.Mazo.Count)
                  .Append('|').Append(p.Tierras.Count).Append('|').Append(p.Seres.Count)
                  .Append('|').Append(p.Retirados.Count).Append('|').Append(p.Concepto.Count)
                  .Append('|').Append(p.DiaActual).Append('|').Append(p.EffectsBlockedTurns)
                  .Append('|').Append(p.NegatedNextEffect).Append('|').Append(p.DiaBlockedTurns)
                  .Append('|').Append(string.Join(",", p.Seres.Cards.Select(c => c.DurLeft)))
                  .Append('#');
            return sb.ToString();
        }
    }
}
