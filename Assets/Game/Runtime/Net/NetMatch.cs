using System;
using Unity.Collections;
using Unity.Netcode;

namespace Game.Runtime.Net
{
    /// <summary>
    /// Relé de la partida 1v1 vía mensajes con nombre (CustomMessagingManager), sin objetos de red
    /// (evita el problema del hash de prefabs creados en runtime). El host fija la semilla y arranca;
    /// cada comando del cliente va al servidor, que lo reenvía a AMBOS en el mismo orden (lockstep).
    /// </summary>
    public static class NetRelay
    {
        private const string MsgStart = "tgb_start"; // servidor -> clientes: semilla
        private const string MsgCmd = "tgb_cmd";     // cliente -> servidor: comando
        private const string MsgApply = "tgb_apply"; // servidor -> clientes: comando ordenado
        private const string MsgHello = "tgb_hello"; // ambos lados: nombre del dispositivo

        private static bool _registered;

        /// <summary>Nombre del dispositivo rival, recibido al conectar (para mostrarlo en el menú).</summary>
        public static string RemoteName { get; private set; }
        public static event Action<string> OnRemoteName;

        public static void Register()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.CustomMessagingManager == null || _registered) return;
            _registered = true;
            var m = nm.CustomMessagingManager;
            m.RegisterNamedMessageHandler(MsgStart, OnStart);
            m.RegisterNamedMessageHandler(MsgCmd, OnCmd);
            m.RegisterNamedMessageHandler(MsgApply, OnApply);
            m.RegisterNamedMessageHandler(MsgHello, OnHello);
        }

        public static void Unregister() { _registered = false; RemoteName = null; }

        /// <summary>Anuncia el nombre local del dispositivo al otro lado de la conexión.</summary>
        public static void SendHello(string name)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.CustomMessagingManager == null) return;
            if (name.Length > 63) name = name.Substring(0, 63);
            using var w = new FastBufferWriter(FastBufferWriter.GetWriteSize(name), Allocator.Temp);
            w.WriteValueSafe(name);
            if (nm.IsServer)
            {
                foreach (var id in nm.ConnectedClientsIds)
                    if (id != NetworkManager.ServerClientId)
                        nm.CustomMessagingManager.SendNamedMessage(MsgHello, id, w);
            }
            else
            {
                nm.CustomMessagingManager.SendNamedMessage(MsgHello, NetworkManager.ServerClientId, w);
            }
        }

        private static void OnHello(ulong sender, FastBufferReader r)
        {
            r.ReadValueSafe(out string name);
            RemoteName = name;
            OnRemoteName?.Invoke(RemoteName);
        }

        /// <summary>El host arranca la partida (fija semilla, avisa a ambos lados).</summary>
        public static void HostStart()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            ulong seed = (ulong)DateTime.UtcNow.Ticks;

            using (var w = new FastBufferWriter(sizeof(ulong), Allocator.Temp))
            {
                w.WriteValueSafe(seed);
                foreach (var id in nm.ConnectedClientsIds)
                    if (id != NetworkManager.ServerClientId)
                        nm.CustomMessagingManager.SendNamedMessage(MsgStart, id, w);
            }
            BeginLocal(seed, 0); // el host es P0
        }

        private static void OnStart(ulong sender, FastBufferReader r)
        {
            r.ReadValueSafe(out ulong seed);
            BeginLocal(seed, 1); // el cliente es P1
        }

        private static void BeginLocal(ulong seed, int myPlayer)
        {
            NetPlay.Active = true;
            NetPlay.Seed = seed;
            NetPlay.MyPlayer = myPlayer;
            NetPlay.Incoming.Clear();
            NetPlay.Submit = SubmitLocal;
            NetPlay.Launch?.Invoke();
        }

        // --- relay de comandos ---

        private static void SubmitLocal(NetCommand c)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;
            if (nm.IsServer) { Broadcast(c); return; }           // host: aplica y reenvía
            using var w = NewWriter(c);                            // cliente: manda al servidor
            nm.CustomMessagingManager.SendNamedMessage(MsgCmd, NetworkManager.ServerClientId, w);
        }

        private static void OnCmd(ulong sender, FastBufferReader r) => Broadcast(ReadCmd(r)); // solo servidor

        private static void Broadcast(NetCommand c)
        {
            var nm = NetworkManager.Singleton;
            NetPlay.Incoming.Enqueue(c); // el host lo aplica localmente
            using var w = NewWriter(c);
            foreach (var id in nm.ConnectedClientsIds)
                if (id != NetworkManager.ServerClientId)
                    nm.CustomMessagingManager.SendNamedMessage(MsgApply, id, w);
        }

        private static void OnApply(ulong sender, FastBufferReader r) => NetPlay.Incoming.Enqueue(ReadCmd(r));

        // --- (de)serialización manual del comando ---

        private static FastBufferWriter NewWriter(NetCommand c)
        {
            var w = new FastBufferWriter(8, Allocator.Temp);
            w.WriteValueSafe((byte)c.Cmd);
            w.WriteValueSafe(c.InstanceId);
            w.WriteValueSafe(c.Flag);
            return w;
        }

        private static NetCommand ReadCmd(FastBufferReader r)
        {
            r.ReadValueSafe(out byte cmd);
            r.ReadValueSafe(out int id);
            r.ReadValueSafe(out byte flag);
            return new NetCommand((NetCmd)cmd, id, flag);
        }
    }
}
