using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Game.Core.Model;

namespace Game.Runtime
{
    /// <summary>
    /// Loader del catálogo para Unity, usando Newtonsoft (com.unity.nuget.newtonsoft-json).
    /// Equivalente al CatalogLoader de dev (System.Text.Json), sobre los mismos modelos del Core.
    /// </summary>
    public static class UnityCatalogLoader
    {
        private static readonly string[] CardArrayKeys =
        {
            "DIA", "TIERRA", "SER_DIVINO", "SER_HUMANO", "SER_ANIMAL", "CONCEPTO"
        };

        /// <summary>Texto del catálogo, multiplataforma: Resources (funciona en Android) y, si no,
        /// StreamingAssets por archivo (editor/standalone).</summary>
        public static string? DefaultJson()
        {
            var ta = Resources.Load<TextAsset>("catalogo_v3");
            if (ta != null) return ta.text;
            var path = System.IO.Path.Combine(Application.streamingAssetsPath, "catalogo.v3.json");
            return System.IO.File.Exists(path) ? System.IO.File.ReadAllText(path) : null;
        }

        /// <summary>Carga el catálogo por defecto (o null si no se encuentra).</summary>
        public static CardCatalog? LoadDefault()
        {
            var json = DefaultJson();
            return json != null ? FromJson(json) : null;
        }

        public static CardCatalog FromJson(string json)
        {
            var root = JObject.Parse(json);
            var cards = new List<CardDefinition>();
            var catalogo = (JObject)root["catalogo_cartas"]!;

            foreach (var key in CardArrayKeys)
            {
                if (catalogo[key] is not JArray arr) continue;
                var type = CardTypeNames.FromJsonKey(key);
                foreach (var el in arr) cards.Add(ParseCard(el, type));
            }

            return new CardCatalog(cards, ParseHistorias(root), ParseDias(root));
        }

        private static CardDefinition ParseCard(JToken el, CardType type) => new CardDefinition(
            id: S(el, "id")!, nombre: S(el, "nombre") ?? "", type: type,
            coste: I(el, "coste"), fd: I(el, "fd"), dur: I(el, "dur"), actCost: I(el, "actCost"),
            turnsLeft: I(el, "turns_left"), especial: B(el, "especial"),
            tipoConcepto: S(el, "tipo"), efecto: S(el, "efecto"), alEntrar: S(el, "al_entrar"),
            activado: S(el, "activado"), alSalir: S(el, "al_salir"),
            condicion: S(el, "condicion"), nota: S(el, "nota"));

        private static List<HistoriaDef> ParseHistorias(JObject root)
        {
            var list = new List<HistoriaDef>();
            if (root["historias"] is not JArray arr) return list;
            foreach (var el in arr)
            {
                var piezas = new List<string>();
                if (el["piezas"] is JArray p)
                    foreach (var x in p) piezas.Add(x.Value<string>() ?? "");
                list.Add(new HistoriaDef(S(el, "id") ?? "", S(el, "nombre") ?? "",
                    ParseMode(S(el, "modo_victoria")), piezas, S(el, "nota")));
            }
            return list;
        }

        private static List<DiaDef> ParseDias(JObject root)
        {
            var list = new List<DiaDef>();
            if (root["dias"] is not JArray arr) return list;
            foreach (var el in arr)
                list.Add(new DiaDef(I(el, "numero") ?? 0, S(el, "nombre") ?? "",
                    I(el, "coste") ?? 0, S(el, "condicion") ?? "", S(el, "efecto") ?? ""));
            return list;
        }

        private static VictoryMode ParseMode(string? s)
            => s == "on_piece_play" ? VictoryMode.OnPiecePlay : VictoryMode.FieldAtEntrega;

        private static string? S(JToken t, string n)
            => t[n]?.Type == JTokenType.String ? t[n]!.Value<string>() : null;
        private static int? I(JToken t, string n)
            => t[n]?.Type == JTokenType.Integer ? t[n]!.Value<int>() : (int?)null;
        private static bool B(JToken t, string n)
            => t[n]?.Type == JTokenType.Boolean && t[n]!.Value<bool>();
    }
}
