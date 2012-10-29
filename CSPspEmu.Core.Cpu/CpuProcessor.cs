﻿using System;
using System.Collections.Generic;
using CSPspEmu.Core.Memory;
using CSharpUtils.Threading;

namespace CSPspEmu.Core.Cpu
{
    public sealed class CpuProcessor : PspEmulatorComponent
	{
		public readonly Dictionary<string, uint> GlobalInstructionStats = new Dictionary<string, uint>();

		public PspConfig PspConfig;

		[Inject]
		public PspMemory Memory;

		[Inject]
		public MethodCacheFast MethodCache;

		private Dictionary<int, Action<CpuThreadState, int>> RegisteredNativeSyscalls;
		public HashSet<uint> NativeBreakpoints;
		public bool IsRunning;
		public bool RunningCallback;
		public CoroutinePool CoroutinePool;

		public PspEmulatorContext GetPspEmulatorContext()
		{
			return PspEmulatorContext;
		}

		public override void InitializeComponent()
		{
			this.PspConfig = PspEmulatorContext.PspConfig;
			if (this.PspConfig.UseCoRoutines)
			{
				this.CoroutinePool = new CoroutinePool();
			}
			else
			{
			}
			NativeBreakpoints = new HashSet<uint>();
			RegisteredNativeSyscalls = new Dictionary<int, Action<CpuThreadState, int>>();
			IsRunning = true;
		}

		public CpuProcessor RegisterNativeSyscall(int Code, Action Callback)
		{
			return RegisterNativeSyscall(Code, (_Code, _Processor) => Callback());
		}

		public CpuProcessor RegisterNativeSyscall(int Code, Action<CpuThreadState, int> Callback)
		{
			RegisteredNativeSyscalls[Code] = Callback;
			return this;
		}

		public Action<CpuThreadState, int> GetSyscall(int Code)
		{
			Action<CpuThreadState, int> Callback;
			if (RegisteredNativeSyscalls.TryGetValue(Code, out Callback))
			{
				return Callback;
			}
			else
			{
				return null;
			}
		}

		public void Syscall(int Code, CpuThreadState CpuThreadState)
		{
			Action<CpuThreadState, int> Callback;
			if ((Callback = GetSyscall(Code)) != null)
			{
				Callback(CpuThreadState, Code);
			}
			else
			{
				Console.WriteLine("Undefined syscall: {0:X6} at 0x{1:X8}", Code, CpuThreadState.PC);
			}
		}

		public void sceKernelDcacheWritebackInvalidateAll()
		{
		}

		public void sceKernelDcacheWritebackRange(uint Address, uint Size)
		{
		}

		public void sceKernelDcacheWritebackInvalidateRange(uint Address, uint Size)
		{
		}

		public void sceKernelDcacheInvalidateRange(uint Address, uint Size)
		{
		}

		public void sceKernelDcacheWritebackAll()
		{
		}

		public void sceKernelIcacheInvalidateAll()
		{
			MethodCache.Clear();
		}

		public void sceKernelIcacheInvalidateRange(uint Address, uint Size)
		{
			//Console.Error.WriteLine("sceKernelIcacheInvalidateRange!!! (0x{0:X}, {1})", Address, Size);
			MethodCache.ClearRange(Address, Address + Size);
			//MethodCache.Clear();
		}

		public event Action DebugCurrentThreadEvent;

		public static void DebugCurrentThread(CpuThreadState CpuThreadState)
		{
			var CpuProcessor = CpuThreadState.CpuProcessor;
			Console.Error.WriteLine("*******************************************");
			Console.Error.WriteLine("* DebugCurrentThread **********************");
			Console.Error.WriteLine("*******************************************");
			CpuProcessor.DebugCurrentThreadEvent();
			Console.Error.WriteLine("*******************************************");
			CpuThreadState.DumpRegisters();
			Console.Error.WriteLine("*******************************************");
		}
	}
}
