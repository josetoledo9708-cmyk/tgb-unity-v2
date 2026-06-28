using System.Collections.Generic;

namespace Game.Core.Model
{
    public enum ZoneId
    {
        Mano,
        Mazo,
        Tierras,
        Seres,
        Concepto,
        Retirados,
        PilaDia,
        Historia
    }

    /// <summary>Una zona del campo de un jugador: lista ordenada con capacidad opcional.</summary>
    public sealed class Zone
    {
        public ZoneId Id { get; }
        public int Capacity { get; } // -1 = ilimitada
        public List<CardInstance> Cards { get; } = new();

        public Zone(ZoneId id, int capacity = -1)
        {
            Id = id;
            Capacity = capacity;
        }

        public int Count => Cards.Count;
        public bool IsFull => Capacity >= 0 && Cards.Count >= Capacity;

        public void Add(CardInstance c) => Cards.Add(c);
        public bool Remove(CardInstance c) => Cards.Remove(c);

        public CardInstance? Top => Cards.Count > 0 ? Cards[0] : null;

        public CardInstance DrawTop()
        {
            var c = Cards[0];
            Cards.RemoveAt(0);
            return c;
        }
    }
}
