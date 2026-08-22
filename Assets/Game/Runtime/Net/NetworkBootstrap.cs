using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Game.Runtime.Net
{
    /// <summary>
    /// Crea/gestiona el NetworkManager en tiempo de ejecución (sin objetos de escena) y ofrece
    /// Host/Cliente por LAN (IP directa). Registra el prefab de <see cref="NetMatch"/> y lo spawnea
    /// cuando el servidor arranca.
    /// </summary>
    public static class NetworkBootstrap
    {
        public const ushort DefaultPort = 7777;

        private static GameObject _nmGo;
        private static bool _hooked;

        public static string Status { get; private set; } = "Desconectado";
        public static bool Running => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        public static int ClientCount =>
            NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer
                ? NetworkManager.Singleton.ConnectedClientsIds.Count : 0;

        private static UnityTransport Ensure()
        {
            if (NetworkManager.Singleton == null)
            {
                _nmGo = new GameObject("NetworkManager");
                Object.DontDestroyOnLoad(_nmGo);
                var nm = _nmGo.AddComponent<NetworkManager>();
                var utp = _nmGo.AddComponent<UnityTransport>();
                nm.NetworkConfig = new NetworkConfig
                {
                    NetworkTransport = utp,
                    ConnectionApproval = false,
                    EnableSceneManagement = false,
                };
            }

            if (!_hooked)
            {
                var nm = NetworkManager.Singleton;
                nm.OnServerStarted += () => { NetRelay.Register(); Refresh(); };
                nm.OnClientConnectedCallback += _ => { NetRelay.Register(); Refresh(); };
                nm.OnClientDisconnectCallback += _ => Refresh();
                _hooked = true;
            }
            return NetworkManager.Singleton.GetComponent<UnityTransport>();
        }

        public static void StartHost(ushort port = DefaultPort)
        {
            var utp = Ensure();
            utp.SetConnectionData("0.0.0.0", port, "0.0.0.0");
            NetworkManager.Singleton.StartHost();
            Status = $"Host en puerto {port} (esperando rival)";
        }

        public static void StartClient(string ip, ushort port = DefaultPort)
        {
            var utp = Ensure();
            utp.SetConnectionData(string.IsNullOrWhiteSpace(ip) ? "127.0.0.1" : ip.Trim(), port);
            NetworkManager.Singleton.StartClient();
            Status = $"Conectando a {ip}:{port}…";
        }

        public static void Shutdown()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                NetworkManager.Singleton.Shutdown();
            NetRelay.Unregister();
            NetPlay.Reset();
            Status = "Desconectado";
        }

        private static void Refresh()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) { Status = "Desconectado"; return; }
            if (nm.IsServer) Status = ClientCount >= 2 ? "Rival conectado — listos" : "Esperando rival…";
            else if (nm.IsConnectedClient) Status = "Conectado al host";
        }
    }
}
