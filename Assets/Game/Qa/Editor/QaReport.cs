using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Game.Qa.Editor
{
    /// <summary>Herramientas del sistema de pruebas. Viven DENTRO de la carpeta de QA para que se
    /// vayan con ella al borrarla.</summary>
    public static class QaReport
    {
        [MenuItem("The Great Book/QA Efectos/Escribir informe")]
        public static void Write()
        {
            QaStore.Reload();
            var d = QaStore.Data;

            var problemas = d.Entries.Where(e => e.AsVerdict() == QaVerdict.Problem).ToList();
            var pendientes = d.Entries.Where(e => e.AsVerdict() == QaVerdict.Pending).ToList();
            var correctos = d.Entries.Where(e => e.AsVerdict() == QaVerdict.Ok).ToList();

            // "Jugados y todavía sin marcar": sale de las JUGADAS guardadas, no del catálogo entero.
            // Las cartas que nunca han salido no son una lista de trabajo, son el catálogo.
            var conVeredicto = new System.Collections.Generic.HashSet<string>(d.Entries.Select(e => e.Key));
            var jugadosSinMarcar = d.Plays
                .Select(p => p.What)
                .Distinct()
                .Where(w => !conVeredicto.Any(k => w.Contains(k.Split('|')[0])))
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine("# Informe de pruebas de efectos — The Great Book");
            sb.AppendLine();
            sb.AppendLine($"Generado: {System.DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"Archivo: `{QaStore.Path}`");
            sb.AppendLine();
            sb.AppendLine($"- Con problema: **{problemas.Count}**");
            sb.AppendLine($"- Pendientes: **{pendientes.Count}**");
            sb.AppendLine($"- Correctos: **{correctos.Count}**");
            sb.AppendLine($"- Jugadas guardadas: {d.Plays.Count}");
            sb.AppendLine();

            void Bloque(string titulo, System.Collections.Generic.List<QaEntry> lista, bool conProblema)
            {
                sb.AppendLine($"## {titulo} ({lista.Count})");
                sb.AppendLine();
                if (lista.Count == 0) { sb.AppendLine("_(ninguno)_"); sb.AppendLine(); return; }
                foreach (var e in lista.OrderBy(x => x.CardId))
                {
                    sb.AppendLine($"- **{e.CardName}** (`{e.CardId}`) · {e.Trigger}");
                    sb.AppendLine($"  - Texto: {e.EffectText}");
                    if (conProblema) sb.AppendLine($"  - **Falla:** {e.Problem}");
                    sb.AppendLine($"  - Probado: {e.When}");
                }
                sb.AppendLine();
            }

            Bloque("Con problema", problemas, true);
            Bloque("Pendientes de comprobar", pendientes, false);

            sb.AppendLine($"## Jugados y todavía sin marcar ({jugadosSinMarcar.Count})");
            sb.AppendLine();
            if (jugadosSinMarcar.Count == 0) sb.AppendLine("_(ninguno)_");
            else foreach (var w in jugadosSinMarcar) sb.AppendLine($"- {w}");
            sb.AppendLine();

            Bloque("Correctos", correctos, false);

            sb.AppendLine("## El campo en cada problema");
            sb.AppendLine();
            foreach (var e in problemas)
            {
                sb.AppendLine($"### {e.CardName} (`{e.CardId}`) · {e.Trigger}");
                sb.AppendLine("```");
                sb.AppendLine(e.Board);
                sb.AppendLine("```");
            }
            sb.AppendLine();

            sb.AppendLine("## Últimas jugadas");
            sb.AppendLine();
            foreach (var p in d.Plays.AsEnumerable().Reverse().Take(40))
                sb.AppendLine($"- `{p.When}` {p.What}");

            string ruta = Path.Combine(Application.persistentDataPath, "qa_informe.md");
            File.WriteAllText(ruta, sb.ToString());
            Debug.Log($"[QA] Informe escrito en: {ruta}\n" +
                      $"Problemas: {problemas.Count} · Pendientes: {pendientes.Count} · Correctos: {correctos.Count}");
            EditorUtility.RevealInFinder(ruta);
        }

        [MenuItem("The Great Book/QA Efectos/Abrir carpeta de resultados")]
        public static void Reveal() => EditorUtility.RevealInFinder(QaStore.Path);

        [MenuItem("The Great Book/QA Efectos/Borrar resultados")]
        public static void ClearResults()
        {
            if (!EditorUtility.DisplayDialog("Borrar resultados de QA",
                    "Se borran TODOS los veredictos y jugadas guardadas. No se puede deshacer.",
                    "Borrar", "Cancelar")) return;
            QaStore.Clear();
            Debug.Log("[QA] Resultados borrados.");
        }
    }
}
