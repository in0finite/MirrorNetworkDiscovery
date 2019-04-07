using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Net;

namespace Mirror
{
	
    public class NetworkDiscoveryHUD : MonoBehaviour
    {
        List<NetworkDiscovery.DiscoveryInfo> m_discoveredServers = new List<NetworkDiscovery.DiscoveryInfo>();
        Coroutine m_coroutine;
        string[] m_headerNames = new string[]{"IP", NetworkDiscovery.kMapNameKey, NetworkDiscovery.kNumPlayersKey, 
            NetworkDiscovery.kMaxNumPlayersKey};
        Vector2 m_scrollViewPos = Vector2.zero;
        bool m_isRefreshing = false;
        bool m_displayBroadcastAddresses = false;

        IPEndPoint m_lookupServer = null;   // server that we are currently looking up
        string m_lookupServerIP = "";
        string m_lookupServerPort = NetworkDiscovery.kDefaultServerPort.ToString();

        public int offsetX = 5;
        public int offsetY = 150;
        public int width = 500, height = 400;
        [Range(1, 5)] public float refreshInterval = 3f;



        void OnEnable()
        {
            NetworkDiscovery.onReceivedServerResponse += OnDiscoveredServer;
        }

        void OnDisable()
        {
            NetworkDiscovery.onReceivedServerResponse -= OnDiscoveredServer;
        }

        void Start()
        {
	        
        }

        void OnGUI()
        {
            if (null == NetworkManager.singleton)
                return;
            if (NetworkServer.active || NetworkClient.active)
                return;
            if (!NetworkDiscovery.SupportedOnThisPlatform)
                return;

            int elemWidth = width / m_headerNames.Length - 5;

            GUILayout.BeginArea(new Rect(offsetX, offsetY, width, height));

            if(m_isRefreshing)
            {
                GUILayout.Button("Refreshing...", GUILayout.Height(25), GUILayout.ExpandWidth(false));
            }
            else
            {
                if (GUILayout.Button("Refresh LAN", GUILayout.Height(25), GUILayout.ExpandWidth(false)))
                {
                    Refresh();
                }
            }

            GUILayout.BeginHorizontal();
            m_displayBroadcastAddresses = GUILayout.Toggle(m_displayBroadcastAddresses, "Display broadcast addresses");
            if (m_displayBroadcastAddresses)
            {
                GUILayout.Space(10);
                GUILayout.Label( string.Join( ", ", NetworkDiscovery.GetBroadcastAdresses().Select(ip => ip.ToString()) ) );
            }
            GUILayout.EndHorizontal();

            GUILayout.Label(string.Format("Servers [{0}]:", m_discoveredServers.Count));

            // header
            GUILayout.BeginHorizontal();
            foreach(string str in m_headerNames)
                GUILayout.Button(str, GUILayout.Width(elemWidth));
            GUILayout.EndHorizontal();

            // servers
            m_scrollViewPos = GUILayout.BeginScrollView(m_scrollViewPos);

            foreach(var info in m_discoveredServers)
            {
                GUILayout.BeginHorizontal();

                if( GUILayout.Button(info.EndPoint.Address.ToString(), GUILayout.Width(elemWidth)) )
                    Connect(info);

                for( int i = 1; i < m_headerNames.Length; i++ )
                {
                    if (info.KeyValuePairs.ContainsKey(m_headerNames[i]))
                        GUILayout.Label(info.KeyValuePairs[m_headerNames[i]], GUILayout.Width(elemWidth));
                    else
                        GUILayout.Label("", GUILayout.Width(elemWidth));
                }

                GUILayout.EndHorizontal();
            }

            GUILayout.EndScrollView();

            // lookup a server

            GUILayout.Label("Lookup server: ");
            GUILayout.BeginHorizontal();
            GUILayout.Label("IP:");
            m_lookupServerIP = GUILayout.TextField(m_lookupServerIP);
            GUILayout.Label("Port:");
            m_lookupServerPort = GUILayout.TextField(m_lookupServerPort);
            if (GUILayout.Button("Lookup"))
                LookupServer();
            GUILayout.EndHorizontal();


            GUILayout.EndArea();

        }

        void Refresh()
        {
            m_discoveredServers.Clear();
            if(m_coroutine != null)
                StopCoroutine(m_coroutine);
            m_coroutine = StartCoroutine(RefreshCoroutine());
        }

        void LookupServer()
        {
            // parse IP and port

            IPAddress ip = IPAddress.Parse(m_lookupServerIP);
            ushort port = ushort.Parse(m_lookupServerPort);

            // input is ok
            // send discovery request

            m_lookupServer = new IPEndPoint(ip, port);

            NetworkDiscovery.SendDiscoveryRequest(m_lookupServer);
        }

        void Connect(NetworkDiscovery.DiscoveryInfo info)
        {
            if (null == NetworkManager.singleton)
                return;
            if (null == Transport.activeTransport)
                return;
            if (!(Transport.activeTransport is TelepathyTransport))
            {
                Debug.LogErrorFormat("Only {0} is supported", typeof(TelepathyTransport));
                return;
            }

            // assign address and port
            NetworkManager.singleton.networkAddress = info.EndPoint.Address.ToString();
            ((TelepathyTransport) Transport.activeTransport).port = ushort.Parse( info.KeyValuePairs[NetworkDiscovery.kPortKey] );

            NetworkManager.singleton.StartClient();
        }

        IEnumerator RefreshCoroutine()
        {
            m_isRefreshing = true;
            yield return null;
            try {
                NetworkDiscovery.SendBroadcast();
            } catch (System.Exception ex) {
                Debug.LogException(ex);
            }
            yield return new WaitForSecondsRealtime(refreshInterval);
            m_isRefreshing = false;
        }

        void OnDiscoveredServer(NetworkDiscovery.DiscoveryInfo info)
        {
            if (!m_isRefreshing && (null == m_lookupServer || !m_lookupServer.Equals(info.EndPoint)))
                return;

            int index = m_discoveredServers.FindIndex(item => item.EndPoint.Equals(info.EndPoint));
            if(index < 0)
            {
                // server is not in the list
                // add it
                m_discoveredServers.Add(info);
            }
            else
            {
                // server is in the list
                // update it
                m_discoveredServers[index] = info;
            }

        }

    }

}
