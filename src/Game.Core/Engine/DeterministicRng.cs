using System.Collections.Generic;

namespace Game.Core.Engine
{
    /// <summary>
    /// RNG determinista y serializable (xorshift64*). El estado es público para poder
    /// guardar/restaurar la partida y reproducir barajados exactos (replays, red).
    /// </summary>
    public sealed class DeterministicRng
    {
        public ulong State { get; private set; }

        public DeterministicRng(ulong seed)
        {
            // Evitar estado 0 (xorshift se queda atascado en 0).
            State = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;
        }

        public ulong NextULong()
        {
            ulong x = State;
            x ^= x >> 12;
            x ^= x << 25;
            x ^= x >> 27;
            State = x;
            return x * 0x2545F4914F6CDD1DUL;
        }

        /// <summary>Entero en [0, maxExclusive).</summary>
        public int NextInt(int maxExclusive)
        {
            if (maxExclusive <= 0) return 0;
            return (int)(NextULong() % (ulong)maxExclusive);
        }

        /// <summary>Baraja in-place (Fisher-Yates) de forma determinista.</summary>
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = NextInt(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }
}
