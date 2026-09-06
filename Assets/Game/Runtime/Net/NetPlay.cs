using System.Collections.Generic;

namespace Game.Runtime.Net
{
    /// <summary>
    /// Puente estático entre la capa de red (NetMatch) y el campo (HotseatView) / menú (MenuShell),
    /// sin que ninguno dependa directamente del otro. La partida en red es LOCKSTEP: los comandos
    /// del jugador local NO se aplican al instante; se envían al servidor, éste los ordena y los
    /// reenvía a AMBOS clientes, que los aplican en el mismo orden (motor determinista + misma semilla).
    /// </summary>
    public static class NetPlay
    {
        public static bool Active;                       // ¿la partida actual es en red?
        public static ulong Seed;                        // semilla común (la fija el host)
        public static int MyPlayer;                      // 0 = host, 1 = cliente

        public static System.Action<NetCommand> Submit;  // HotseatView -> red (enviar comando local)
        public static System.Action Launch;              // red -> menú (encender el campo al empezar)
        public static System.Action PeerDisconnected;    // red -> campo: el rival se desconectó a media partida

        // Comandos ya ordenados por el servidor, pendientes de aplicar por el campo (ambos lados).
        public static readonly Queue<NetCommand> Incoming = new();

        public static void Reset()
        {
            Active = false;
            MyPlayer = 0; // vuelve al lado local por defecto (una partida local es siempre P0)
            Submit = null;
            Incoming.Clear();
        }
    }
}
