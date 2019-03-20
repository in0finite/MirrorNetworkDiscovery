using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Mirror
{
	
    public class NetworkDiscoveryHUD : MonoBehaviour
    {
        List<NetworkDiscovery.DiscoveryInfo> m_discoveredServers = new List<NetworkDiscovery.DiscoveryInfo>();
        Coroutine m_coroutine;
        string[] m_headerNames = new string[]{"IP", NetworkDiscovery.kMapNameKey, NetworkDiscovery.kNumPlayersKey, 
            NetworkDiscovery.kMaxNumPlayersKey};
        Vector2 m_scrollViewPos = Vector2.zero;

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

            if (GUILayout.Button("Refresh LAN", GUILayout.Height(25), GUILayout.ExpandWidth(false)))
            {
                Refresh();
            }

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

            GUILayout.EndArea();

        }

        void Refresh()
        {
            m_discoveredServers.Clear();
            if(m_coroutine != null)
                StopCoroutine(m_coroutine);
            m_coroutine = StartCoroutine(RefreshCoroutine());
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
            yield return null;
            NetworkDiscovery.SendBroadcast();
            yield return new WaitForSecondsRealtime(refreshInterval);
        }

        void OnDiscoveredServer(NetworkDiscovery.DiscoveryInfo info)
        {
            m_discoveredServers.Add(info);
        }

    }

}
