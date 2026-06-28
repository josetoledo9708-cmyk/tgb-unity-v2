using System.Collections.Generic;
using System.Linq;
using Game.Core.Model;

namespace Game.Core.Data
{
    public sealed class ValidationResult
    {
        public bool Ok => Errors.Count == 0;
        public List<string> Errors { get; } = new();
        public void Add(string e) => Errors.Add(e);
        public override string ToString()
            => Ok ? "OK" : string.Join("; ", Errors);
    }

    /// <summary>
    /// Reglas: 40-50 cartas principales; máx 3 copias por carta; todos los ids existen y son
    /// jugables (no DIA/HISTORIA); HISTORIA válida; el mazo incluye las 5 piezas (por nombre).
    /// </summary>
    public static class DeckValidator
    {
        public const int MinCards = 40;
        public const int MaxCards = 50;
        public const int MaxCopies = 3;

        public static ValidationResult Validate(DeckDefinition deck, CardCatalog catalog)
        {
            var r = new ValidationResult();

            int n = deck.MainCardIds.Count;
            if (n < MinCards || n > MaxCards)
                r.Add($"El mazo debe tener entre {MinCards} y {MaxCards} cartas (tiene {n}).");

            // Copias por id.
            foreach (var g in deck.MainCardIds.GroupBy(id => id))
                if (g.Count() > MaxCopies)
                    r.Add($"'{g.Key}' tiene {g.Count()} copias (máx {MaxCopies}).");

            // Existencia y tipo jugable.
            foreach (var id in deck.MainCardIds.Distinct())
            {
                if (!catalog.TryGet(id, out var def))
                {
                    r.Add($"Id desconocido en el mazo: '{id}'.");
                    continue;
                }
                if (def.Type == CardType.Dia || def.Type == CardType.Historia)
                    r.Add($"'{id}' ({def.Type}) no puede ir en el mazo principal.");
            }

            // HISTORIA válida.
            var historia = catalog.FindHistoria(deck.HistoriaId);
            if (historia == null)
            {
                r.Add($"HISTORIA desconocida: '{deck.HistoriaId}'.");
                return r;
            }

            // Las 5 piezas (por nombre) deben estar en el mazo.
            var nombresEnMazo = deck.MainCardIds
                .Where(id => catalog.TryGet(id, out _))
                .Select(id => catalog.Get(id).Nombre)
                .ToHashSet();

            foreach (var pieza in historia.Piezas)
                if (!nombresEnMazo.Contains(pieza))
                    r.Add($"Falta la pieza '{pieza}' de la HISTORIA {historia.Id}.");

            return r;
        }
    }
}
