﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CSharpUtils.Factory;
using System.Reflection;

namespace CSPspEmu.Core
{
	public class PspConfig
	{
		static public string CultureName = "en";

		public bool VerticalSynchronization = true;
		public bool TraceJIT = false;
		public bool CountInstructionsAndYield = true;

		//public bool ShowInstructionStats = true;
		public bool ShowInstructionStats = false;

		/// <summary>
		/// Specifies if the current emulator has a display.
		/// This will be used for the automated tests.
		/// </summary>
		public bool HasDisplay = true;
		public bool DebugSyscalls = false;
		public int CpuFrequency = 222;

		//public bool NoticeUnimplementedGpuCommands = true;
		public bool NoticeUnimplementedGpuCommands = false;

		public bool UseFastAndUnsaferMemory = true;
		//public bool UseFastAndUnsaferMemory = false;

		/// <summary>
		/// Writes a line each time a JAL is executed
		/// </summary>
		public bool TraceJal = false;
		//public bool TraceJal = true;

		/// <summary>
		/// 
		/// </summary>
		public bool DebugThreadSwitching = false;
		//public bool DebugThreadSwitching = true;

		/// <summary>
		/// 
		/// </summary>
		public Assembly HleModulesDll;

		public PspConfig()
		{
		}

		//public bool TraceJal = true;
	}
}
