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
    }
}
