using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Game.Qa
{
    /// <summary>Veredicto de un efecto. "Pendiente" es el que hace que el sistema se use: sin él,
    /// quien prueba acaba pulsando "sí" por no dejar el cuadro abierto.</summary>
    public enum QaVerdict { Ok, Problem, Pending }

    [Serializable]
    public sealed class QaEntry
    {
        public string Key = "";         // <cardId>|<trigger>
        public string CardId = "";
        public string CardName = "";
        public string Trigger = "";
        public string EffectText = "";  // el texto impreso: para leer el informe sin buscar la carta
        public string Verdict = nameof(QaVerdict.Pending);
        public string Problem = "";
        public string When = "";
        public string Board = "";

        public QaVerdict AsVerdict()
            => Enum.TryParse<QaVerdict>(Verdict, out var v) ? v : QaVerdict.Pending;

        /// <summary>Ok y Problem son definitivos: no se vuelve a preguntar. Pending sí.</summary>
        public bool IsFinal() => AsVerdict() != QaVerdict.Pending;
    }

    [Serializable]
    public sealed class QaPlay
    {
        public string When = "";
        public string What = "";   // descripción de la jugada, con el id de carta entre paréntesis
        public string Board = "";
    }

    [Serializable]
    public sealed class QaData
    {
        public List<QaEntry> Entries = new();
        public List<QaPlay> Plays = new();
        public List<string> RetiredDecks = new();
    }

    /// <summary>
    /// Archivo de resultados: un solo JSON en la carpeta de datos del usuario, FUERA del proyecto.
    /// Se guarda en cada respuesta y con cada jugada apuntada, porque una sesión de pruebas se corta
    /// a lo bruto muy a menudo.
    /// </summary>
    public static class QaStore
    {
        public const int MaxPlays = 200;
        public const string FileName = "qa_feedback.json";

        private static QaData _data;
        private static readonly object Lock = new();

        public static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        public static QaData Data
        {
            get { lock (Lock) { return _data ??= Load(); } }
        }

        private static QaData Load()
        {
            try
            {
                if (File.Exists(Path))
                {
                    var json = File.ReadAllText(Path);
                    var d = JsonUtility.FromJson<QaData>(json);
                    if (d != null) return d;
                }
            }
            catch (Exception e) { Debug.LogWarning("[QA] No se pudo leer " + Path + ": " + e.Message); }
            return new QaData();
        }

        /// <summary>Vuelve a leer el archivo del disco (tras editarlo o borrarlo fuera del juego).</summary>
        public static void Reload() { lock (Lock) { _data = Load(); } }

        public static void Save()
        {
            try
            {
                lock (Lock)
                {
                    File.WriteAllText(Path, JsonUtility.ToJson(_data ??= new QaData(), true));
                }
            }
            catch (Exception e) { Debug.LogWarning("[QA] No se pudo guardar " + Path + ": " + e.Message); }
        }

        public static QaEntry Find(string key)
            => Data.Entries.FirstOrDefault(e => e.Key == key);

        /// <summary>Guarda el veredicto de un efecto (crea la entrada si no existía) y persiste.</summary>
        public static void SetVerdict(QaEntry plantilla, QaVerdict verdict, string problema)
        {
            lock (Lock)
            {
                var d = _data ??= Load();
                var e = d.Entries.FirstOrDefault(x => x.Key == plantilla.Key);
                if (e == null) { e = plantilla; d.Entries.Add(e); }

                e.CardId = plantilla.CardId;
                e.CardName = plantilla.CardName;
                e.Trigger = plantilla.Trigger;
                e.EffectText = plantilla.EffectText;
                e.Board = plantilla.Board;
                e.Verdict = verdict.ToString();
                e.Problem = verdict == QaVerdict.Problem ? (problema ?? "") : "";
                e.When = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            }
            Save();
        }

        /// <summary>Apunta una jugada CON la foto del campo: un "no funciona" sin saber qué había
        /// en el campo no se reproduce. Se apuntan todas, se pregunte o no.</summary>
        public static void AddPlay(string what, string board)
        {
            lock (Lock)
            {
                var d = _data ??= Load();
                d.Plays.Add(new QaPlay
                {
                    When = DateTime.Now.ToString("HH:mm:ss"),
                    What = what ?? "",
                    Board = board ?? "",
                });
                if (d.Plays.Count > MaxPlays) d.Plays.RemoveRange(0, d.Plays.Count - MaxPlays);
            }
            Save();
        }

        public static void RetireDeck(string deckId)
        {
            lock (Lock)
            {
                var d = _data ??= Load();
                if (!d.RetiredDecks.Contains(deckId)) d.RetiredDecks.Add(deckId);
            }
            Save();
        }

        public static void Clear()
        {
            lock (Lock) { _data = new QaData(); }
            Save();
        }
    }
}
