using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEngine;

namespace Game.Runtime.Net
{
    /// <summary>
    /// Descubrimiento LAN por código compartido: el host emite broadcasts UDP con su código y nombre
    /// de dispositivo; el cliente escucha y se conecta automáticamente al primero que coincida.
    /// Tick() debe llamarse cada frame desde el hilo principal (sin hilos: LAN doméstica, latencia
    /// irrelevante).
    /// </summary>
    public static class LanDiscovery
    {
        private const int Port = 47777;
        private const float BroadcastInterval = 1f;

        private static UdpClient _broadcaster;
        private static UdpClient _listener;
        private static float _nextBroadcast;
        private static string _code;

        public static string LocalDeviceName => string.IsNullOrEmpty(SystemInfo.deviceName) || SystemInfo.deviceName == "<unknown>"
            ? Environment.MachineName : SystemInfo.deviceName;

        public static string FoundIp { get; private set; }
        public static string FoundDeviceName { get; private set; }
        public static bool IsSearching => _listener != null;

        public static void StartHostBroadcast(string code)
        {
            StopAll();
            _code = code;
            _broadcaster = new UdpClient { EnableBroadcast = true };
            _nextBroadcast = 0f; // emite ya en el primer Tick
        }

        public static void StartClientSearch(string code)
        {
            StopAll();
            _code = code;
            try
            {
                _listener = new UdpClient(Port) { EnableBroadcast = true };
            }
            catch (Exception e)
            {
                Debug.LogWarning("LanDiscovery: no se pudo escuchar puerto " + Port + ": " + e.Message);
            }
        }

        public static void StopAll()
        {
            _broadcaster?.Close(); _broadcaster = null;
            _listener?.Close(); _listener = null;
            FoundIp = null; FoundDeviceName = null;
        }

        /// <summary>Cierra el socket de escucha del cliente sin borrar FoundIp/FoundDeviceName
        /// (se llama justo después de auto-conectar, para poder seguir mostrando el nombre hallado).</summary>
        public static void StopListening()
        {
            _listener?.Close(); _listener = null;
        }

        /// <summary>Llamar cada frame. Host: reemite su presencia. Cliente: procesa broadcasts recibidos.</summary>
        public static void Tick()
        {
            if (_broadcaster != null && Time.unscaledTime >= _nextBroadcast)
            {
                _nextBroadcast = Time.unscaledTime + BroadcastInterval;
                var msg = $"TGB|{_code}|{LocalDeviceName}";
                var data = Encoding.UTF8.GetBytes(msg);
                try { _broadcaster.Send(data, data.Length, new IPEndPoint(IPAddress.Broadcast, Port)); }
                catch (Exception e) { Debug.LogWarning("LanDiscovery broadcast: " + e.Message); }
            }

            if (_listener != null)
            {
                try
                {
                    while (_listener.Available > 0)
                    {
                        var ep = new IPEndPoint(IPAddress.Any, 0);
                        var data = _listener.Receive(ref ep);
                        var msg = Encoding.UTF8.GetString(data);
                        var parts = msg.Split('|');
                        if (parts.Length == 3 && parts[0] == "TGB" && parts[1] == _code)
                        {
                            FoundIp = ep.Address.ToString();
                            FoundDeviceName = parts[2];
                        }
                    }
                }
                catch (Exception e) { Debug.LogWarning("LanDiscovery listen: " + e.Message); }
            }
        }
    }
}
