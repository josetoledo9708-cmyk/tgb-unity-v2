namespace Game.Core.Model
{
    /// <summary>
    /// Una carta concreta dentro de una partida (un molde CardDefinition + estado mutable).
    /// </summary>
    public sealed class CardInstance
    {
        public int InstanceId { get; }
        public CardDefinition Def { get; }
        public int OwnerId { get; }

        // Estado mutable en campo.
        public bool Tapped { get; set; }
        public bool FaceDown { get; set; }
        public int DurLeft { get; set; }            // SER: turnos de duración restantes.
        public int TurnsLeftRemaining { get; set; } // TIERRA con autodestrucción (Sodoma/Gomorra).
        public int ProtectedUntilTurn { get; set; } = -1; // protección de destrucción (nº de turno global).
        public bool Indestructible { get; set; }          // no puede ser destruida por ningún efecto.

        public CardInstance(int instanceId, CardDefinition def, int ownerId)
        {
            InstanceId = instanceId;
            Def = def;
            OwnerId = ownerId;
            DurLeft = def.Dur ?? 0;
            TurnsLeftRemaining = def.TurnsLeft ?? 0;
        }

        public CardType Type => Def.Type;
        public string Nombre => Def.Nombre;

        public override string ToString()
            => $"#{InstanceId} {Def.Nombre}" + (Tapped ? " (tap)" : "");
    }
}
