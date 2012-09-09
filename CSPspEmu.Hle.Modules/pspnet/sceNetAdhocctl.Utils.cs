﻿namespace CSPspEmu.Hle.Modules.pspnet
{
	public unsafe partial class sceNetAdhocctl
	{
		/// <summary>
		/// Product structure
		/// </summary>
		public struct productStruct
		{
			/// <summary>
			/// Unknown, set to 0
			/// </summary>
			public int unknown;
			
			/// <summary>
			/// The product ID string
			/// </summary>
			public fixed byte product[9];
		}

		/// <summary>
		/// Peer info structure
		/// </summary>
		public struct SceNetAdhocctlPeerInfo
		{
			/// <summary>
			/// 
			/// </summary>
			//public SceNetAdhocctlPeerInfo *next;
			public uint nextPointer;
			
			/// <summary>
			/// Nickname
			/// </summary>
			public fixed byte nickname[128];	
			
			/// <summary>
			/// Mac address
			/// </summary>
			public fixed byte mac[6];
			
			/// <summary>
			/// Unknown
			/// </summary>
			public fixed byte unknown[6];
			
			/// <summary>
			/// Time stamp
			/// </summary>
			public uint timestamp;
		}

		/// <summary>
		/// Scan info structure
		/// </summary>
		public struct SceNetAdhocctlScanInfo
		{
			/// <summary>
			/// 
			/// </summary>
			//SceNetAdhocctlScanInfo *next;
			public uint nextPointer;

			/// <summary>
			/// Channel number
			/// </summary>
			public int channel;
			
			/// <summary>
			/// Name of the connection (alphanumeric characters only)
			/// </summary>
			public fixed byte name[8];
			
			/// <summary>
			/// The BSSID
			/// </summary>
			public fixed byte bssid[6];
			
			/// <summary>
			/// Unknown
			/// </summary>
			public fixed byte unknown[2];
			
			/// <summary>
			/// Unknown
			/// </summary>
			public int unknown2;
		}

		public struct SceNetAdhocctlGameModeInfo
		{
			/// <summary>
			/// Number of peers (including self)
			/// </summary>
			public int count;

			/// <summary>
			/// MAC addresses of peers (including self)
			/// </summary>
			//byte macs[16][6];
			public fixed byte macs[16 * 6];
		}

		/// <summary>
		/// Params structure
		/// </summary>
		public struct SceNetAdhocctlParams
		{
			/// <summary>
			/// Channel number
			/// </summary>
			public int channel;
			
			/// <summary>
			/// Name of the connection
			/// </summary>
			public fixed byte name[8];
			
			/// <summary>
			/// The BSSID
			/// </summary>
			public fixed byte bssid[6];
			
			/// <summary>
			/// Nickname
			/// </summary>
			public fixed byte nickname[128];
		}
	}
}
