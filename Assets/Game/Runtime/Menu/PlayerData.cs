using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime.Menu
{
    [Serializable]
    public sealed class DeckEntry
    {
        public string nombre = "Mazo";
        public string historiaId = "h1";
        public List<string> cartas = new();
        public string insignia = ""; // id de la carta insignia que representa el mazo
    }

    [Serializable]
    public sealed class DeckList { public List<DeckEntry> mazos = new(); }

    /// <summary>Datos persistentes del jugador (monedas, mazos). Guardado simple en PlayerPrefs.</summary>
    public static class PlayerData
    {
        private const string CoinsKey = "tgb_monedas";
        private const string DecksKey = "tgb_mazos";
        private const string TomosKey = "tgb_tomos";

        public static int Monedas
        {
            get => PlayerPrefs.GetInt(CoinsKey, 500);
            set { PlayerPrefs.SetInt(CoinsKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        /// <summary>Tomos (sobres) por abrir. Arranca con 10 disponibles.</summary>
        public static int Tomos
        {
            get => PlayerPrefs.GetInt(TomosKey, 10);
            set { PlayerPrefs.SetInt(TomosKey, Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static List<DeckEntry> Mazos()
        {
            var json = PlayerPrefs.GetString(DecksKey, "");
            if (string.IsNullOrEmpty(json)) return new List<DeckEntry>();
            try { return JsonUtility.FromJson<DeckList>(json)?.mazos ?? new List<DeckEntry>(); }
            catch { return new List<DeckEntry>(); }
        }

        public static void SaveMazos(List<DeckEntry> mazos)
        {
            PlayerPrefs.SetString(DecksKey, JsonUtility.ToJson(new DeckList { mazos = mazos }));
            PlayerPrefs.Save();
        }

        // Selección para lanzar partida (leída por el campo si se implementa el hand-off).
        public static string SelectedHistoriaId;
        public static List<string> SelectedDeck;

        /// <summary>COSTURA de pruebas: mazos extra que aparecen en las listas de selección.
        /// El menú los lista sin saber quién los pone; sin sistema de QA vale null y no hay nada.</summary>
        public static Func<List<DeckEntry>> ExtraDecks;

        /// <summary>Progreso a mostrar bajo el nombre de un mazo ("12/34 efectos"). Costura de QA.</summary>
        public static Func<DeckEntry, string> ExtraDeckProgress;

        /// <summary>v0.01 — Mazos de PRUEBA predefinidos (2 variantes por cada una de las 7 historias).
        /// No se guardan en PlayerPrefs: se generan al vuelo desde StoryDecks/SampleDeckBuilder, así
        /// siempre reflejan el código y no se pueden borrar por accidente. Entre todos cubren el
        /// catálogo completo, que es lo que permite probar cada carta y sus interacciones.</summary>
        public static List<DeckEntry> MazosPredefinidos(Game.Core.Model.CardCatalog catalog)
        {
            var lista = new List<DeckEntry>();
            if (catalog == null) return lista;

            foreach (var historia in catalog.Historias)
                for (int variante = 0; variante < Game.Core.Data.StoryDecks.Variants; variante++)
                {
                    if (Game.Core.Data.StoryDecks.For(historia.Id, variante) == null) continue;
                    var deck = Game.Core.Data.SampleDeckBuilder.Build(catalog, historia.Id, 40, 3, variante);
                    lista.Add(new DeckEntry
                    {
                        nombre = $"{historia.Nombre} · {(char)('A' + variante)}",
                        historiaId = historia.Id,
                        cartas = new List<string>(deck.MainCardIds),
                        insignia = deck.MainCardIds.Count > 0 ? deck.MainCardIds[0] : "",
                    });
                }
            return lista;
        }
    }
}
