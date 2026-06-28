using System;
using System.Collections.Generic;
using System.Text.Json;
using Game.Core.Model;

namespace Game.Core.Data
{
    /// <summary>
    /// Carga el catálogo desde el JSON (data/catalogo.v3.json) a un CardCatalog.
    /// Usa System.Text.Json (dev/test). En Unity se reemplaza por un loader equivalente
    /// (JsonUtility/Newtonsoft) sobre los mismos modelos.
    /// </summary>
    public static class CatalogLoader
    {
        private static readonly string[] CardArrayKeys =
        {
            "DIA", "TIERRA", "SER_DIVINO", "SER_HUMANO", "SER_ANIMAL", "CONCEPTO"
        };

        public static CardCatalog FromJson(string json)
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var cards = new List<CardDefinition>();
            var catalogo = root.GetProperty("catalogo_cartas");

            foreach (var key in CardArrayKeys)
            {
                if (!catalogo.TryGetProperty(key, out var arr)) continue;
                var type = CardTypeNames.FromJsonKey(key);
                foreach (var el in arr.EnumerateArray())
                    cards.Add(ParseCard(el, type));
            }

            var historias = ParseHistorias(root);
            var dias = ParseDias(root);

            return new CardCatalog(cards, historias, dias);
        }

        private static CardDefinition ParseCard(JsonElement el, CardType type)
        {
            return new CardDefinition(
                id: GetString(el, "id") ?? throw new FormatException("Carta sin id"),
                nombre: GetString(el, "nombre") ?? "",
                type: type,
                coste: GetInt(el, "coste"),
                fd: GetInt(el, "fd"),
                dur: GetInt(el, "dur"),
                actCost: GetInt(el, "actCost"),
                turnsLeft: GetInt(el, "turns_left"),
                especial: GetBool(el, "especial") ?? false,
                tipoConcepto: GetString(el, "tipo"),
                efecto: GetString(el, "efecto"),
                alEntrar: GetString(el, "al_entrar"),
                activado: GetString(el, "activado"),
                alSalir: GetString(el, "al_salir"),
                condicion: GetString(el, "condicion"),
                nota: GetString(el, "nota"));
        }

        private static List<HistoriaDef> ParseHistorias(JsonElement root)
        {
            var list = new List<HistoriaDef>();
            if (!root.TryGetProperty("historias", out var arr)) return list;

            foreach (var el in arr.EnumerateArray())
            {
                var piezas = new List<string>();
                if (el.TryGetProperty("piezas", out var p))
                    foreach (var piece in p.EnumerateArray())
                        piezas.Add(piece.GetString() ?? "");

                list.Add(new HistoriaDef(
                    id: GetString(el, "id") ?? "",
                    nombre: GetString(el, "nombre") ?? "",
                    modoVictoria: ParseVictoryMode(GetString(el, "modo_victoria")),
                    piezas: piezas,
                    nota: GetString(el, "nota")));
            }
            return list;
        }

        private static List<DiaDef> ParseDias(JsonElement root)
        {
            var list = new List<DiaDef>();
            if (!root.TryGetProperty("dias", out var arr)) return list;

            foreach (var el in arr.EnumerateArray())
            {
                list.Add(new DiaDef(
                    numero: GetInt(el, "numero") ?? 0,
                    nombre: GetString(el, "nombre") ?? "",
                    coste: GetInt(el, "coste") ?? 0,
                    condicion: GetString(el, "condicion") ?? "",
                    efecto: GetString(el, "efecto") ?? ""));
            }
            return list;
        }

        private static VictoryMode ParseVictoryMode(string? s) => s switch
        {
            "on_piece_play" => VictoryMode.OnPiecePlay,
            _ => VictoryMode.FieldAtEntrega
        };

        // --- helpers de lectura opcional ---

        private static string? GetString(JsonElement el, string name)
            => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString()
                : null;

        private static int? GetInt(JsonElement el, string name)
            => el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number
                ? v.GetInt32()
                : (int?)null;

        private static bool? GetBool(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var v)) return null;
            return v.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => (bool?)null
            };
        }
    }
}
