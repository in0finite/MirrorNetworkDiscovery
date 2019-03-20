using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using UnityEngine.Profiling;

namespace Mirror
{
	
	public class NetworkDiscovery : MonoBehaviour
	{
		
		public class DiscoveryInfo
		{
			private IPEndPoint endPoint;
			private Dictionary<string, string> keyValuePairs = new Dictionary<string, string> ();
			private float timeWhenReceived = 0f;
			public DiscoveryInfo (IPEndPoint endPoint, Dictionary<string, string> keyValuePairs)
			{
				this.endPoint = endPoint;
				this.keyValuePairs = keyValuePairs;
				this.timeWhenReceived = Time.realtimeSinceStartup;
			}
			public IPEndPoint EndPoint { get { return this.endPoint; } }
			public Dictionary<string, string> KeyValuePairs { get { return this.keyValuePairs; } }
			public float TimeSinceReceived { get { return Time.realtimeSinceStartup - this.timeWhenReceived; } }
		}

		public static event System.Action<DiscoveryInfo> onReceivedServerResponse = delegate {};

		// server sends this data as a response to broadcast
		static Dictionary<string, string> m_responseData = new Dictionary<string, string> ();

		public static NetworkDiscovery singleton { get ; private set ; }

		public const string kSignatureKey = "Signature", kPortKey = "Port", kNumPlayersKey = "Players", 
			kMaxNumPlayersKey = "MaxNumPlayers", kMapNameKey = "Map";

	//	[SerializeField] int m_clientPort = 18417;
		[SerializeField] int m_serverPort = 18418;
		static UdpClient m_serverUdpCl = null;
		static UdpClient m_clientUdpCl = null;

		public bool simulateResponding = false;

		static string m_signature = null;

		public static bool SupportedOnThisPlatform { get { return Application.platform != RuntimePlatform.WebGLPlayer; } }

		static bool IsServerActive { get { return NetworkServer.active; } }
	//	static bool IsClientActive { get { return NetworkClient.active; } }
		public int gameServerPortNumber = 7777;
		static int NumPlayers { get { return NetworkServer.connections.Count; } }
		static int MaxNumPlayers { get { return NetworkManager.singleton != null ? NetworkManager.singleton.maxConnections : 0; } }



		void Awake ()
		{
			if (singleton != null)
				return;

			singleton = this;

		}

		void Start ()
		{
			if(!SupportedOnThisPlatform)
				return;

			StartCoroutine (ClientCoroutine ());

			StartCoroutine (ServerCoroutine ());

			StartCoroutine (SimulateRespondingCoroutine ());

		}

		void OnDisable ()
		{
			ShutdownUdpClients ();
		}


		/// <summary>
		/// Simulates responses from server.
		/// </summary>
		static System.Collections.IEnumerator SimulateRespondingCoroutine()
		{

			yield return new WaitForSecondsRealtime (4f);

			// generate some random IPs
			string[] ips = new string[15];
			for (int i = 0; i < ips.Length; i++) {
				string ip = Random.Range( 1, 255 ) + "." + Random.Range( 1, 255 ) + "." + Random.Range( 1, 255 ) + "." + Random.Range( 1, 255 ) ;
				ips [i] = ip;
			}

			while (true) {

				yield return null;

				float pauseTime = Random.Range (0.1f, 0.7f);
				yield return new WaitForSecondsRealtime (pauseTime);

				if (!singleton.simulateResponding)
					continue;

				string ip = ips [Random.Range (0, ips.Length - 1)];

				var dict = new Dictionary<string, string> () {
					{ kSignatureKey, GetSignature() },
					{ kPortKey, Random.Range (1, 65535).ToString () },
					{ kNumPlayersKey, Random.Range (0, 20).ToString () },
					{ kMaxNumPlayersKey, Random.Range (30, 100).ToString () },
					{ kMapNameKey, "arena" }
				};

				OnReceivedServerResponse( new DiscoveryInfo( new IPEndPoint(IPAddress.Parse(ip), Random.Range (1, 65535)), dict ) );
			}

		}


		void Update ()
		{
			if (!SupportedOnThisPlatform)
				return;

			if (IsServerActive)
			{
				UpdateResponseData ();
			}

			if(IsServerActive)
			{
				// make sure server's UDP client is created
				EnsureServerIsInitialized();
			}
			else
			{
				// we should close server's UDP client
				CloseServerUdpClient();
			}

		}


		static void EnsureServerIsInitialized()
		{

			if (m_serverUdpCl != null)
				return;

			m_serverUdpCl = new UdpClient (singleton.m_serverPort);
			RunSafe( () => { m_serverUdpCl.EnableBroadcast = true; } );

		//	m_serverUdpCl.BeginReceive(new System.AsyncCallback(ReceiveCallback), null);

		}

		static void EnsureClientIsInitialized()
		{

			if (m_clientUdpCl != null)
				return;

			m_clientUdpCl = new UdpClient (0);
			RunSafe( () => { m_clientUdpCl.EnableBroadcast = true; } );
			// turn off receiving from our IP
			RunSafe( () => { m_clientUdpCl.MulticastLoopback = false; } );

		//	m_clientUdpCl.JoinMulticastGroup (IPAddress.Parse("127.0.0.1"));

		}

		static void ShutdownUdpClients()
		{
			CloseServerUdpClient();
			CloseClientUdpClient();
		}

		static void CloseServerUdpClient()
		{
			if (m_serverUdpCl != null) {
				m_serverUdpCl.Close ();
				m_serverUdpCl = null;
			}
		}

		static void CloseClientUdpClient()
		{
			if (m_clientUdpCl != null) {
				m_clientUdpCl.Close ();
				m_clientUdpCl = null;
			}
		}


		static DiscoveryInfo ReadDataFromUdpClient(UdpClient udpClient)
		{
			
			// only proceed if there is available data in network buffer, or otherwise Receive() will block
			// average time for UdpClient.Available : 10 us
			if (udpClient.Available <= 0)
				return null;
			
			Profiler.BeginSample("UdpClient.Receive");
			IPEndPoint remoteEP = new IPEndPoint (IPAddress.Any, 0);
			byte[] receivedBytes = udpClient.Receive (ref remoteEP);
			Profiler.EndSample ();

			if (remoteEP != null && receivedBytes != null && receivedBytes.Length > 0) {

				Profiler.BeginSample ("Convert data");
				var dict = ConvertByteArrayToDictionary (receivedBytes);
				Profiler.EndSample ();

				return new DiscoveryInfo(remoteEP, dict);
			}

			return null;
		}

		static System.Collections.IEnumerator ServerCoroutine()
		{

			while (true)
			{

				yield return new WaitForSecondsRealtime (0.3f);

				if (null == m_serverUdpCl)
					continue;

				if(!IsServerActive)
					continue;
				
				// average time for this (including data receiving and processing): less than 100 us
				Profiler.BeginSample ("Receive broadcast");
			//	var timer = System.Diagnostics.Stopwatch.StartNew ();

				RunSafe (() =>
				{
					var info = ReadDataFromUdpClient(m_serverUdpCl);
					if(info != null)
						OnReceivedBroadcast(info);
				});

			//	Debug.Log("receive broadcast time: " + timer.GetElapsedMicroSeconds () + " us");
				Profiler.EndSample ();

			}

		}

		static void OnReceivedBroadcast(DiscoveryInfo info)
		{
			if(info.KeyValuePairs.ContainsKey(kSignatureKey) && info.KeyValuePairs[kSignatureKey] == GetSignature())
			{
				// signature matches
				// send response

				Profiler.BeginSample("Send response");
				byte[] bytes = ConvertDictionaryToByteArray( m_responseData );
				m_serverUdpCl.Send( bytes, bytes.Length, info.EndPoint );
				Profiler.EndSample();
			}
		}

		static System.Collections.IEnumerator ClientCoroutine()
		{

			while (true)
			{
				yield return new WaitForSecondsRealtime (0.3f);

				if (null == m_clientUdpCl)
					continue;
				
				RunSafe( () =>
				{
					var info = ReadDataFromUdpClient (m_clientUdpCl);
					if (info != null)
						OnReceivedServerResponse(info);
				});
				
			}

		}

		public static void SendBroadcast()
		{
			if (!SupportedOnThisPlatform)
				return;

			EnsureClientIsInitialized();

			if (null == m_clientUdpCl)
				return;
			
			// TODO: data should be broadcasted to internal networks only ? e.g. those that start with 192


			Profiler.BeginSample("ConvertDictionaryToByteArray");
			var dict = new Dictionary<string, string>() {{kSignatureKey, GetSignature()}};
			byte[] buffer = ConvertDictionaryToByteArray (dict);
			Profiler.EndSample();

			IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, singleton.m_serverPort);

			Profiler.BeginSample("UdpClient.Send");
			try {
				m_clientUdpCl.Send (buffer, buffer.Length, endPoint);
			} catch(SocketException ex) {
				if(ex.ErrorCode == 10051) {
					// Network is unreachable
					// ignore this error

				} else {
					throw;
				}
			}
			Profiler.EndSample();

		}


		static void OnReceivedServerResponse(DiscoveryInfo info) {

			// check if data is valid
			if(!IsDataFromServerValid(info))
				return;
			
			// invoke event
			onReceivedServerResponse(info);

		}

		public static bool IsDataFromServerValid(DiscoveryInfo data)
		{
			// data must contain signature which matches, and port number
			return data.KeyValuePairs.ContainsKey(kSignatureKey) && data.KeyValuePairs[kSignatureKey] == GetSignature()
				&& data.KeyValuePairs.ContainsKey(kPortKey);
		}


		public static void RegisterResponseData( string key, string value )
		{
			m_responseData[key] = value;
		}

		public static void UnRegisterResponseData( string key )
		{
			m_responseData.Remove (key);
		}

		/// <summary>
		/// Adds/updates some default response data.
		/// </summary>
		public static void UpdateResponseData()
		{
			
			RegisterResponseData (kSignatureKey, GetSignature());
			RegisterResponseData (kPortKey, singleton.gameServerPortNumber.ToString());
			RegisterResponseData (kNumPlayersKey, NumPlayers.ToString ());
			RegisterResponseData (kMaxNumPlayersKey, MaxNumPlayers.ToString ());
			RegisterResponseData (kMapNameKey, UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);

		}

		/// Signature identifies this game among others.
		public static string GetSignature()
		{
			if (m_signature != null)
				return m_signature;
			
			string[] strings = new string[]{ Application.companyName, Application.productName, Application.version, 
				Application.unityVersion };

			m_signature = "";

			foreach(string str in strings)
			{
				// only use it's hash code
				m_signature += str.GetHashCode() + ".";
			}

			return m_signature;
		}


		public static string ConvertDictionaryToString( Dictionary<string, string> dict )
		{
			return string.Join( "\n", dict.Select( pair => pair.Key + ": " + pair.Value ) );
		}

		public static Dictionary<string, string> ConvertStringToDictionary( string str )
		{
			var dict = new Dictionary<string, string>();
			string[] lines = str.Split("\n".ToCharArray(), System.StringSplitOptions.RemoveEmptyEntries);
			foreach(string line in lines)
			{
				int index = line.IndexOf(": ");
				if(index < 0)
					continue;
				dict[line.Substring(0, index)] = line.Substring(index + 2, line.Length - index - 2);
			}
			return dict;
		}

		public static byte[] ConvertDictionaryToByteArray( Dictionary<string, string> dict )
		{
			return ConvertStringToPacketData( ConvertDictionaryToString( dict ) );
		}

		public static Dictionary<string, string> ConvertByteArrayToDictionary( byte[] data )
		{
			return ConvertStringToDictionary( ConvertPacketDataToString( data ) );
		}

		public static byte[] ConvertStringToPacketData(string str)
		{
			return System.Text.Encoding.UTF8.GetBytes(str);
		}

		public static string ConvertPacketDataToString(byte[] data)
		{
			return System.Text.Encoding.UTF8.GetString(data);
		}


		static bool RunSafe(System.Action action)
		{
			try
			{
				action();
				return true;
			}
			catch(System.Exception ex)
			{
				Debug.LogException(ex);
				return false;
			}
		}

	}

}
