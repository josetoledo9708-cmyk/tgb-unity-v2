using System.Linq;
using Game.Core.Effects;
using Game.Core.Model;
using UnityEditor;
using UnityEngine;

namespace Game.Qa.Editor
{
    /// <summary>
    /// Comprobación del propio sistema de pruebas. Son diez minutos de trabajo y cazan los fallos
    /// clásicos: que un aplazado vuelva a preguntar, que las claves no choquen y que el archivo se
    /// relea bien. Va como menú (y no como test de NUnit) para que se borre con la carpeta sin
    /// dejar tests colgando en la suite del juego.
    /// </summary>
    public static class QaSelfTests
    {
        [MenuItem("The Great Book/QA Efectos/Comprobar el propio sistema")]
        public static void Run()
        {
            int fallos = 0;
            void Check(bool ok, string que)
            {
                if (ok) Debug.Log("[QA-self] OK — " + que);
                else { Debug.LogError("[QA-self] FALLA — " + que); fallos++; }
            }

            var catalog = Game.Runtime.UnityCatalogLoader.LoadDefault();
            Check(catalog != null, "el catálogo se carga");
            if (catalog == null) return;
            var resolver = CardEffects.BuildResolver();

            // 1) Cada efecto registrado produce una clave única.
            var claves = catalog.Cards.Values
                .SelectMany(d => QaEffects.Of(d, resolver))
                .Select(e => e.Key)
                .ToList();
            Check(claves.Count == claves.Distinct().Count(), "las claves de efecto no se repiten");
            Check(claves.Count > 0, $"hay efectos que comprobar ({claves.Count})");

            // 2) Toda clave tiene la forma <cardId>|<trigger>.
            Check(claves.All(k => k.Count(c => c == '|') == 1), "el formato de clave es <carta>|<trigger>");

            // 3) Todo efecto comprobable trae texto para enseñar en el cuadro.
            var sinTexto = catalog.Cards.Values
                .SelectMany(d => QaEffects.Of(d, resolver))
                .Where(e => string.IsNullOrWhiteSpace(e.Text))
                .Select(e => e.Key)
                .ToList();
            Check(sinTexto.Count == 0, "todos los efectos traen texto: " + (sinTexto.Count == 0 ? "sí" : string.Join(", ", sinTexto)));

            // 4) Los veredictos se comportan: Ok/Problem son definitivos, Pending vuelve a preguntar.
            Check(new QaEntry { Verdict = nameof(QaVerdict.Ok) }.IsFinal(), "Ok es definitivo");
            Check(new QaEntry { Verdict = nameof(QaVerdict.Problem) }.IsFinal(), "Problem es definitivo");
            Check(!new QaEntry { Verdict = nameof(QaVerdict.Pending) }.IsFinal(), "Pending vuelve a preguntar");

            // 5) Guardar y releer conserva el veredicto (sin tocar los resultados reales).
            var antes = QaStore.Data.Entries.Count;
            const string clavePrueba = "__selftest__|AlEntrar";
            QaStore.SetVerdict(new QaEntry
            {
                Key = clavePrueba, CardId = "__selftest__", CardName = "Prueba",
                Trigger = "AlEntrar", EffectText = "texto", Board = "campo",
            }, QaVerdict.Problem, "motivo de prueba");
            QaStore.Reload();
            var leido = QaStore.Find(clavePrueba);
            Check(leido != null && leido.AsVerdict() == QaVerdict.Problem && leido.Problem == "motivo de prueba",
                  "el archivo se guarda y se relee con el veredicto");

            // limpieza: la entrada de prueba no debe quedarse en los resultados
            QaStore.Data.Entries.RemoveAll(e => e.Key == clavePrueba);
            QaStore.Save();
            QaStore.Reload();
            Check(QaStore.Find(clavePrueba) == null && QaStore.Data.Entries.Count == antes,
                  "la entrada de prueba se limpia");

            Debug.Log(fallos == 0
                ? "[QA-self] Todo correcto."
                : $"[QA-self] {fallos} comprobación(es) fallidas.");
        }
    }
}
