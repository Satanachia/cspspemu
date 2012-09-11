﻿using System;
using CSPspEmu.Core;
using CSPspEmu.Core.Cpu;
using CSPspEmu.Hle.Attributes;
using CSPspEmu.Hle.Managers;

namespace CSPspEmu.Hle.Modules.emulator
{
	[HlePspModule(ModuleFlags = ModuleFlags.UserMode | ModuleFlags.Flags0x00010011)]
	public unsafe class Emulator : HleModuleHost
	{
		[Inject]
		HleThreadManager ThreadManager;

		[HlePspFunction(NID = 0x00000000, FirmwareVersion = 150)]
		public void emitInt(int Value)
		{
			Console.WriteLine("emitInt: {0}", Value);
		}

		[HlePspFunction(NID = 0x00000001, FirmwareVersion = 150)]
		public void emitFloat(float Value)
		{
			Console.WriteLine("emitFloat: {0:0.00000}", Value);
		}

		[HlePspFunction(NID = 0x00000002, FirmwareVersion = 150)]
		public void emitString(string Value)
		{
			Console.WriteLine("emitString: '{0}'", Value);
		}

		[HlePspFunction(NID = 0x00000003, FirmwareVersion = 150)]
		public void emitMemoryBlock(byte* Value, uint Size)
		{
			throw(new NotImplementedException());
		}

		[HlePspFunction(NID = 0x00000004, FirmwareVersion = 150)]
		public void emitHex(byte* Value, uint Size)
		{
			throw(new NotImplementedException());
		}

		[HlePspFunction(NID = 0x00000005, FirmwareVersion = 150)]
		public void emitUInt(uint Value)
		{
			Console.WriteLine("emitUInt: {0}", "0x%08X".Sprintf(Value));
		}

		[HlePspFunction(NID = 0x00000006, FirmwareVersion = 150)]
		public void emitLong(long Value)
		{
			Console.WriteLine("emitLong: {0}", "0x%016X".Sprintf(Value));
		}

		[HlePspFunction(NID = 0x10000010, FirmwareVersion = 150)]
		public long testArguments(int Argument1, long Argument2, float Argument3)
		{
			return (long)Argument1 + (long)Argument2 + (long)Argument3;
		}

		[HlePspFunction(NID = 0x10000000, FirmwareVersion = 150)]
		public void waitThreadForever(CpuThreadState CpuThreadState)
		{
			var SleepThread = ThreadManager.Current;
			SleepThread.SetStatus(HleThread.Status.Waiting);
			SleepThread.CurrentWaitType = HleThread.WaitType.None;
			ThreadManager.Yield();
		}

		[HlePspFunction(NID = 0x10000001, FirmwareVersion = 150)]
		public void finalizeCallback(CpuThreadState CpuThreadState)
		{
			CpuThreadState.CpuProcessor.RunningCallback = false;
			CpuThreadState.Yield();
			//throw (new HleEmulatorFinalizeCallbackException());
		}
	}
}
