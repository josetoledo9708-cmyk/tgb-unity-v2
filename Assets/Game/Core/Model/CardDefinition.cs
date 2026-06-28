namespace Game.Core.Model
{
    /// <summary>
    /// Molde inmutable de una carta del catálogo. No es una instancia en partida
    /// (eso es CardInstance, en un milestone posterior). Los campos opcionales valen
    /// null cuando no aplican al tipo de carta.
    /// </summary>
    public sealed class CardDefinition
    {
        public string Id { get; }
        public string Nombre { get; }
        public CardType Type { get; }

        // Coste en FD (SER, CONCEPTO, DIA). Las TIERRA no tienen coste de juego.
        public int? Coste { get; }
        // FD que genera al tapearse (solo TIERRA).
        public int? Fd { get; }
        // Duración en turnos (solo SER).
        public int? Dur { get; }
        // Coste de activación del efecto activado (solo SER).
        public int? ActCost { get; }
        // Turnos hasta autodestrucción (TIERRA especiales: Sodoma, Gomorra).
        public int? TurnsLeft { get; }
        // TIERRA especial (con efecto) vs básica (solo 1 FD).
        public bool Especial { get; }
        // Subtipo de CONCEPTO: "Uso único" | "RESPUESTA".
        public string? TipoConcepto { get; }

        // Textos de efecto (referencia humana; la lógica vive en EffectRegistry).
        public string? Efecto { get; }
        public string? AlEntrar { get; }
        public string? Activado { get; }
        public string? AlSalir { get; }
        public string? Condicion { get; }
        public string? Nota { get; }

        public CardDefinition(
            string id, string nombre, CardType type,
            int? coste = null, int? fd = null, int? dur = null, int? actCost = null,
            int? turnsLeft = null, bool especial = false, string? tipoConcepto = null,
            string? efecto = null, string? alEntrar = null, string? activado = null,
            string? alSalir = null, string? condicion = null, string? nota = null)
        {
            Id = id;
            Nombre = nombre;
            Type = type;
            Coste = coste;
            Fd = fd;
            Dur = dur;
            ActCost = actCost;
            TurnsLeft = turnsLeft;
            Especial = especial;
            TipoConcepto = tipoConcepto;
            Efecto = efecto;
            AlEntrar = alEntrar;
            Activado = activado;
            AlSalir = alSalir;
            Condicion = condicion;
            Nota = nota;
        }

        public override string ToString() => $"{Id} \"{Nombre}\" ({Type})";
    }
}
