// ---------------------------------------------------------------------------------------------
// ONLINE CO-OP FOUNDATION — guarded by BLASTFRAME_NGO. See ClientNetworkTransform.cs header.
// Written against NGO 1.x; NOT compile-verified (package install blocked by environment TLS error).
// ---------------------------------------------------------------------------------------------
#if BLASTFRAME_NGO
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BlastFrame.Networking
{
    /// <summary>
    /// Minimal LAN/loopback connect controller for the online co-op foundation. Lives in Core next to a
    /// NetworkManager + UnityTransport that are IDLE by default — single-player never starts a session.
    /// Toggle the panel with F10 (F9 is taken by PlayerTestMode). Host / Join(IP) / Disconnect + status.
    /// Default 127.0.0.1:7777.
    ///
    /// NOTE: the panel uses IMGUI (OnGUI) rather than Canvas+TMP. This is a deliberate scope call for
    /// dev-only, unverifiable-in-this-environment tooling; a Canvas+TMP panel is the follow-up once the
    /// package installs and the flow can actually be exercised.
    /// </summary>
    [RequireComponent(typeof(NetworkManager))]
    public class NetworkSessionLauncher : MonoBehaviour
    {
        [Tooltip("Default address the Join button connects to.")]
        [SerializeField] private string defaultAddress = "127.0.0.1";

        [Tooltip("Default port for host/join.")]
        [SerializeField] private ushort defaultPort = 7777;

        [Tooltip("Key that shows/hides the connect panel. F9 is reserved by PlayerTestMode.")]
        [SerializeField] private Key toggleKey = Key.F10;

        private NetworkManager _net;
        private bool _panelVisible;
        private string _address;
        private string _status = "Offline";

        private void Awake()
        {
            _net = GetComponent<NetworkManager>();
            _address = defaultAddress;
        }

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb != null && kb[toggleKey].wasPressedThisFrame)
                _panelVisible = !_panelVisible;
        }

        private void ApplyConnectionData()
        {
            if (_net.NetworkConfig.NetworkTransport is UnityTransport utp)
                utp.SetConnectionData(_address, defaultPort);
        }

        private void OnGUI()
        {
            if (!_panelVisible) return;

            const int w = 260, h = 160;
            GUILayout.BeginArea(new Rect(12, 12, w, h), GUI.skin.box);
            GUILayout.Label("Blast Frame — Online Co-op (LAN)");
            GUILayout.Label($"Status: {_status}");

            if (_net.IsListening)
            {
                GUILayout.Label(_net.IsHost ? "Hosting" : (_net.IsServer ? "Server" : "Client"));
                if (GUILayout.Button("Disconnect"))
                {
                    _net.Shutdown();
                    _status = "Offline";
                }
            }
            else
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label("IP", GUILayout.Width(24));
                _address = GUILayout.TextField(_address);
                GUILayout.EndHorizontal();

                if (GUILayout.Button("Host"))
                {
                    ApplyConnectionData();
                    _status = _net.StartHost() ? "Hosting" : "Host failed";
                }
                if (GUILayout.Button("Join"))
                {
                    ApplyConnectionData();
                    _status = _net.StartClient() ? "Connecting…" : "Join failed";
                }
            }

            GUILayout.EndArea();
        }
    }
}
#endif
