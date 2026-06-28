using System.Collections.Generic;

namespace Game.Core.Model
{
    /// <summary>Carta HISTORIA (condición de victoria), externa al mazo.</summary>
    public sealed class HistoriaDef
    {
        public string Id { get; }
        public string Nombre { get; }
        public VictoryMode ModoVictoria { get; }
        public IReadOnlyList<string> Piezas { get; }
        public string? Nota { get; }

        public HistoriaDef(string id, string nombre, VictoryMode modoVictoria,
                           IReadOnlyList<string> piezas, string? nota = null)
        {
            Id = id;
            Nombre = nombre;
            ModoVictoria = modoVictoria;
            Piezas = piezas;
            Nota = nota;
        }
    }

    /// <summary>Carta DIA (×7, externa al mazo, apiladas en orden).</summary>
    public sealed class DiaDef
    {
        public int Numero { get; }
        public string Nombre { get; }
        public int Coste { get; }
        public string Condicion { get; }
        public string Efecto { get; }

        public DiaDef(int numero, string nombre, int coste, string condicion, string efecto)
        {
            Numero = numero;
            Nombre = nombre;
            Coste = coste;
            Condicion = condicion;
            Efecto = efecto;
        }
    }
}
