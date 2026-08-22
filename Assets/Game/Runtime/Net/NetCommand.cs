using Unity.Netcode;

namespace Game.Runtime.Net
{
    /// <summary>Tipos de acción que un jugador puede enviar por la red.</summary>
    public enum NetCmd : byte
    {
        PlayTierra,   // jugar una TIERRA (InstanceId = carta)
        TapTierra,    // tapear una TIERRA (InstanceId = carta)
        PlaySer,      // jugar un SER (InstanceId = carta)
        PlayConcepto, // jugar un CONCEPTO (Flag: 1 = boca abajo)
        ActivateDia,  // activar el DÍA actual
        ActivateSer,  // activar el efecto de un SER (InstanceId = carta)
        EndTurn,      // terminar el turno
    }

    /// <summary>Comando serializable que viaja por la red (referencia a la carta por InstanceId).</summary>
    public struct NetCommand : INetworkSerializable
    {
        public NetCmd Cmd;
        public int InstanceId;
        public byte Flag;

        public NetCommand(NetCmd cmd, int instanceId = 0, byte flag = 0)
        {
            Cmd = cmd; InstanceId = instanceId; Flag = flag;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> s) where T : IReaderWriter
        {
            s.SerializeValue(ref Cmd);
            s.SerializeValue(ref InstanceId);
            s.SerializeValue(ref Flag);
        }
    }
}
