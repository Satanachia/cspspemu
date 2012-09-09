﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using CSharpUtils;
using CSPspEmu.Core;
using CSPspEmu.Core.Cpu;
using CSPspEmu.Core.Cpu.Assembler;
using CSPspEmu.Core.Memory;
using CSPspEmu.Core.Rtc;
using CSPspEmu.Hle;
using CSPspEmu.Hle.Formats;
using CSPspEmu.Hle.Loader;
using CSPspEmu.Hle.Managers;
using CSPspEmu.Hle.Modules.ctrl;
using CSPspEmu.Hle.Modules.display;
using CSPspEmu.Hle.Modules.emulator;
using CSPspEmu.Hle.Modules.loadexec;
using CSPspEmu.Hle.Modules.threadman;
using CSPspEmu.Hle.Modules.utils;
using CSPspEmu.Hle.Vfs;
using CSPspEmu.Hle.Vfs.Local;
using CSPspEmu.Hle.Vfs.Emulator;
using CSPspEmu.Hle.Vfs.MemoryStick;
using CSPspEmu.Hle.Vfs.Iso;
using CSPspEmu.Hle.Vfs.Zip;
using CSPspEmu.Resources;
using CSPspEmu.Hle.Formats.Archive;

namespace CSPspEmu.Runner.Components.Cpu
{
	public unsafe sealed class CpuComponentThread : ComponentThread
	{
		static Logger Logger = Logger.GetLogger("CpuComponentThread");

		protected override string ThreadName { get { return "CpuThread"; } }

		[Inject]
		public CpuProcessor CpuProcessor;

		[Inject]
		public PspRtc PspRtc;
		
		[Inject]
		public HleThreadManager HleThreadManager;
		
		[Inject]
		public PspMemory PspMemory;

		[Inject]
		public ElfPspLoader Loader;

		[Inject]
		public HleMemoryManager MemoryManager;

		[Inject]
		public HleModuleManager ModuleManager;

		[Inject]
		public HleIoManager HleIoManager;

		[Inject]
		public ThreadManForUser ThreadManForUser;

		[Inject]
		public HleIoDriverEmulator HleIoDriverEmulator;

		public HleIoDriverMountable MemoryStickMountable;

		public AutoResetEvent StoppedEndedEvent = new AutoResetEvent(false);

		public override void InitializeComponent()
		{
			RegisterDevices();
		}

		void RegisterDevices()
		{
			string MemoryStickRootFolder = ApplicationPaths.MemoryStickRootFolder;
			//Console.Error.WriteLine(MemoryStickRootFolder);
			//Console.ReadKey();
			try { Directory.CreateDirectory(MemoryStickRootFolder); }
			catch { }
			/*
			*/

			MemoryStickMountable = new HleIoDriverMountable();
			MemoryStickMountable.Mount("/", new HleIoDriverLocalFileSystem(MemoryStickRootFolder));
			var MemoryStick = new HleIoDriverMemoryStick(PspEmulatorContext, MemoryStickMountable);
			//var MemoryStick = new HleIoDriverMemoryStick(new HleIoDriverLocalFileSystem(VirtualDirectory).AsReadonlyHleIoDriver());

			// http://forums.ps2dev.org/viewtopic.php?t=5680
			HleIoManager.SetDriver("ms:", MemoryStick);
			HleIoManager.SetDriver("fatms:", MemoryStick);
			HleIoManager.SetDriver("fatmsOem:", MemoryStick);
			HleIoManager.SetDriver("mscmhc:", MemoryStick);

			HleIoManager.SetDriver("msstor:", new ReadonlyHleIoDriver(MemoryStick));
			HleIoManager.SetDriver("msstor0p:", new ReadonlyHleIoDriver(MemoryStick));

			HleIoManager.SetDriver("disc:", MemoryStick);
			HleIoManager.SetDriver("umd:", MemoryStick);

			HleIoManager.SetDriver("emulator:", HleIoDriverEmulator);
			HleIoManager.SetDriver("kemulator:", HleIoDriverEmulator);

			HleIoManager.SetDriver("flash:", new HleIoDriverZip(new ZipArchive(ResourceArchive.GetFlash0ZipFileStream())));
		}

		public IsoFile SetIso(string IsoFile)
		{
			//"../../../TestInput/cube.iso"
			var Iso = IsoLoader.GetIso(IsoFile);
			var Umd = new HleIoDriverIso(Iso);
			HleIoManager.SetDriver("disc:", Umd);
			HleIoManager.SetDriver("umd:", Umd);
			HleIoManager.SetDriver("host:", Umd);
			HleIoManager.SetDriver(":", Umd);
			HleIoManager.Chdir("disc0:/PSP_GAME/USRDIR");
			return Iso;
		}

		void SetVirtualFolder(string VirtualDirectory)
		{
			MemoryStickMountable.Mount(
				"/PSP/GAME/virtual",
				new HleIoDriverLocalFileSystem(VirtualDirectory)
					//.AsReadonlyHleIoDriver()
			);
		}

		void RegisterSyscalls()
		{
			new MipsAssembler(new PspMemoryStream(PspMemory)).Assemble(
				@"
					.code CODE_PTR_EXIT_THREAD
						syscall 0x7777
						jr r31
						nop
					.code CODE_PTR_FINALIZE_CALLBACK
						syscall 0x7778
						jr r31
						nop
				"
				.Replace("CODE_PTR_EXIT_THREAD", String.Format("0x{0:X}", HleEmulatorSpecialAddresses.CODE_PTR_EXIT_THREAD))
				.Replace("CODE_PTR_FINALIZE_CALLBACK", String.Format("0x{0:X}", HleEmulatorSpecialAddresses.CODE_PTR_FINALIZE_CALLBACK))
			);

			//var ThreadManForUser = ModuleManager.GetModule<ThreadManForUser>();

			RegisterModuleSyscall<ThreadManForUser>(0x206D, "sceKernelCreateThread");
			RegisterModuleSyscall<ThreadManForUser>(0x206F, "sceKernelStartThread");
			RegisterModuleSyscall<ThreadManForUser>(0x2071, "sceKernelExitDeleteThread");

			RegisterModuleSyscall<UtilsForUser>(0x20BF, "sceKernelUtilsMt19937Init");
			RegisterModuleSyscall<UtilsForUser>(0x20C0, "sceKernelUtilsMt19937UInt");

			RegisterModuleSyscall<sceDisplay>(0x213A, "sceDisplaySetMode");
			RegisterModuleSyscall<sceDisplay>(0x2147, "sceDisplayWaitVblankStart");
			RegisterModuleSyscall<sceDisplay>(0x213F, "sceDisplaySetFrameBuf");

			RegisterModuleSyscall<LoadExecForUser>(0x20EB, "sceKernelExitGame");

			RegisterModuleSyscall<sceCtrl>(0x2150, "sceCtrlPeekBufferPositive");

			RegisterModuleSyscall<Emulator>(0x1010, "emitInt");
			RegisterModuleSyscall<Emulator>(0x1011, "emitFloat");
			RegisterModuleSyscall<Emulator>(0x1012, "emitString");
			RegisterModuleSyscall<Emulator>(0x1013, "emitMemoryBlock");
			RegisterModuleSyscall<Emulator>(0x1014, "emitHex");
			RegisterModuleSyscall<Emulator>(0x1015, "emitUInt");
			RegisterModuleSyscall<Emulator>(0x1016, "emitLong");
			RegisterModuleSyscall<Emulator>(0x1017, "testArguments");
			//RegisterModuleSyscall<Emulator>(0x7777, "waitThreadForever");
			RegisterModuleSyscall<ThreadManForUser>(0x7777, "_hle_sceKernelExitDeleteThread");
			RegisterModuleSyscall<Emulator>(0x7778, "finalizeCallback");
		}

		void RegisterModuleSyscall<TType>(int SyscallCode, string FunctionName) where TType : HleModuleHost
		{
			var Delegate = ModuleManager.GetModuleDelegate<TType>(FunctionName);
			CpuProcessor.RegisterNativeSyscall(SyscallCode, (CpuThreadState, Code) =>
			{
				Delegate(CpuThreadState);
			});
		}

		public void _LoadFile(String FileName)
		{
			//GC.Collect();
			SetVirtualFolder(Path.GetDirectoryName(FileName));

			var MemoryStream = new PspMemoryStream(PspMemory);

			var Arguments = new[] {
				"ms0:/PSP/GAME/virtual/EBOOT.PBP",
			};

			Stream LoadStream = File.OpenRead(FileName);
			//using ()
			{
				List<Stream> ElfLoadStreamTry = new List<Stream>();
				//Stream ElfLoadStream = null;

				var Format = new FormatDetector().DetectSubType(LoadStream);
				String Title = null;
				switch (Format)
				{
					case FormatDetector.SubType.Pbp:
						{
							var Pbp = new Pbp().Load(LoadStream);
							ElfLoadStreamTry.Add(Pbp[Pbp.Types.PspData]);
							try
							{
								var ParamSfo = new Psf().Load(Pbp[Pbp.Types.ParamSfo]);
								Title = (String)ParamSfo.EntryDictionary["TITLE"];
								try
								{
									PspEmulatorContext.PspConfig.SetVersion(ParamSfo.EntryDictionary["PSP_SYSTEM_VER"].ToString());
								}
								catch (Exception Exception)
								{
									Logger.Error(Exception);
								}
							}
							catch (Exception Exception)
							{
								Console.Error.WriteLine(Exception);
							}
						}
						break;
					case FormatDetector.SubType.Elf:
						ElfLoadStreamTry.Add(LoadStream);
						break;
					case FormatDetector.SubType.Dax:
					case FormatDetector.SubType.Cso:
					case FormatDetector.SubType.Iso:
						{
							Arguments[0] = "disc0:/PSP/GAME/SYSDIR/EBOOT.BIN";

							var Iso = SetIso(FileName);
							try
							{
								var ParamSfo = new Psf().Load(Iso.Root.Locate("/PSP_GAME/PARAM.SFO").Open());
								Title = (String)ParamSfo.EntryDictionary["TITLE"];
							}
							catch (Exception Exception)
							{
								Console.Error.WriteLine(Exception);
							}

							var FilesToTry = new[] {
								"/PSP_GAME/SYSDIR/BOOT.BIN",
								"/PSP_GAME/SYSDIR/EBOOT.BIN",
								"/PSP_GAME/SYSDIR/EBOOT.OLD",
							};

							foreach (var FileToTry in FilesToTry)
							{
								try
								{
									ElfLoadStreamTry.Add(Iso.Root.Locate(FileToTry).Open());
								}
								catch
								{
								}
								//if (ElfLoadStream.Length != 0) break;
							}

							/*
							if (ElfLoadStream.Length == 0)
							{
								throw (new Exception(String.Format("{0} files are empty", String.Join(", ", FilesToTry))));
							}
							*/
						}
						break;
					default:
						throw (new NotImplementedException("Can't load format '" + Format + "'"));
				}

				Exception LoadException = null;
				HleModuleGuest HleModuleGuest = null;

				foreach (var ElfLoadStream in ElfLoadStreamTry)
				{
					try
					{
						LoadException = null;

						if (ElfLoadStream.Length < 256) throw(new InvalidProgramException("File too short"));

						HleModuleGuest = Loader.LoadModule(
							ElfLoadStream,
							MemoryStream,
							MemoryManager.GetPartition(HleMemoryManager.Partitions.User),
							ModuleManager,
							Title,
							ModuleName: FileName,
							IsMainModule: true
						);

						LoadException = null;

						break;
					}
					catch (InvalidProgramException Exception)
					{
						LoadException = Exception;
					}
				}

				if (LoadException != null) throw (LoadException);

				RegisterSyscalls();

				uint StartArgumentAddress = 0x08000100;
				uint EndArgumentAddress = StartArgumentAddress;

				var ArgumentsChunk = Arguments
					.Select(Argument => Encoding.UTF8.GetBytes(Argument + "\0"))
					.Aggregate(new byte[] { }, (Accumulate, Chunk) => (byte[])Accumulate.Concat(Chunk))
				;

				var ReservedSyscallsPartition = MemoryManager.GetPartition(HleMemoryManager.Partitions.Kernel0).Allocate(
					0x100,
					Name: "ReservedSyscallsPartition"
				);
				var ArgumentsPartition = MemoryManager.GetPartition(HleMemoryManager.Partitions.Kernel0).Allocate(
					ArgumentsChunk.Length,
					Name: "ArgumentsPartition"
				);
				PspMemory.WriteBytes(ArgumentsPartition.Low, ArgumentsChunk);

				Debug.Assert(ThreadManForUser != null);

				// @TODO: Use Module Manager

				//var MainThread = ThreadManager.Create();
				//var CpuThreadState = MainThread.CpuThreadState;
				var CurrentCpuThreadState = new CpuThreadState(CpuProcessor);
				{
					//CpuThreadState.PC = Loader.InitInfo.PC;
					CurrentCpuThreadState.GP = HleModuleGuest.InitInfo.GP;
					CurrentCpuThreadState.CallerModule = HleModuleGuest;

					int ThreadId = (int)ThreadManForUser.sceKernelCreateThread(CurrentCpuThreadState, "<EntryPoint>", HleModuleGuest.InitInfo.PC, 10, 0x1000, PspThreadAttributes.ClearStack, null);

					//var Thread = HleThreadManager.GetThreadById(ThreadId);
					ThreadManForUser._sceKernelStartThread(CurrentCpuThreadState, ThreadId, ArgumentsPartition.Size, ArgumentsPartition.Low);
					//Console.WriteLine("RA: 0x{0:X}", CurrentCpuThreadState.RA);
				}
				CurrentCpuThreadState.DumpRegisters();
				MemoryManager.GetPartition(HleMemoryManager.Partitions.User).Dump();
				//ModuleManager.LoadedGuestModules.Add(HleModuleGuest);
					
				//MainThread.CurrentStatus = HleThread.Status.Ready;
			}
		}

		private void Main_Ended()
		{
			StoppedEndedEvent.Set();

			// Completed execution. Wait for stopping.
			while (true)
			{
				ThreadTaskQueue.HandleEnqueued();
				if (!Running) return;
				Thread.Sleep(1);
			}
		}

		protected override void Main()
		{
			while (Running)
			{
#if !DO_NOT_PROPAGATE_EXCEPTIONS
				try
#endif
				{
					// HACK! TODO: Update PspRtc every 2 thread switchings.
					// Note: It should update the RTC after selecting the next thread to run.
					// But currently is is not possible since updating the RTC and waking up
					// threads has secondary effects that I have to consideer first.
					bool TickAlternate = false;

					//PspRtc.Update();
					while (true)
					{
						ThreadTaskQueue.HandleEnqueued();
						if (!Running) return;

						if (!TickAlternate) PspRtc.Update();
						TickAlternate = !TickAlternate;

						HleThreadManager.StepNext(DoBeforeSelectingNext : () =>
						{
							//PspRtc.Update();
						});
					}
				}
#if !DO_NOT_PROPAGATE_EXCEPTIONS
				catch (Exception Exception)
				{
					if (Exception is SceKernelSelfStopUnloadModuleException || Exception.InnerException is SceKernelSelfStopUnloadModuleException)
					{
						Console.WriteLine("SceKernelSelfStopUnloadModuleException");
						Main_Ended();
						return;
					}

					var ErrorOut = Console.Error;

					ConsoleUtils.SaveRestoreConsoleState(() =>
					{
						Console.ForegroundColor = ConsoleColor.Red;

						try
						{
							ErrorOut.WriteLine("Error on thread {0}", HleThreadManager.Current);
							try
							{
								ErrorOut.WriteLine(Exception);
							}
							catch
							{
							}

							HleThreadManager.Current.CpuThreadState.DumpRegisters(ErrorOut);

							ErrorOut.WriteLine(
								"Last registered PC = 0x{0:X}, RA = 0x{1:X}, RelocatedBaseAddress=0x{2:X}, UnrelocatedPC=0x{3:X}",
								HleThreadManager.Current.CpuThreadState.PC,
								HleThreadManager.Current.CpuThreadState.RA,
								PspEmulatorContext.PspConfig.RelocatedBaseAddress,
								HleThreadManager.Current.CpuThreadState.PC - PspEmulatorContext.PspConfig.RelocatedBaseAddress
							);

							ErrorOut.WriteLine("Last called syscalls: ");
							foreach (var CalledCallback in ModuleManager.LastCalledCallbacks.Reverse())
							{
								ErrorOut.WriteLine("  {0}", CalledCallback);
							}

							foreach (var Thread in HleThreadManager.Threads)
							{
								ErrorOut.WriteLine("{0}", Thread.ToExtendedString());
								ErrorOut.WriteLine(
									"Last valid PC: 0x{0:X} :, 0x{1:X}",
									Thread.CpuThreadState.LastValidPC,
									Thread.CpuThreadState.LastValidPC - PspEmulatorContext.PspConfig.RelocatedBaseAddress
								);
								Thread.DumpStack(ErrorOut);
							}

							ErrorOut.WriteLine(
								"Executable had relocation: {0}. RelocationAddress: 0x{1:X}",
								PspEmulatorContext.PspConfig.InfoExeHasRelocation,
								PspEmulatorContext.PspConfig.RelocatedBaseAddress
							);

							ErrorOut.WriteLine("");
							ErrorOut.WriteLine("Error on thread {0}", HleThreadManager.Current);
							ErrorOut.WriteLine(Exception);
							ErrorOut.WriteLine("Saved a memory dump to 'error_memorydump.bin'", HleThreadManager.Current);

							var Memory = MemoryManager.Memory;

							Memory.Dump("error_memorydump.bin");
						}
						catch (Exception Exception2)
						{
							Console.WriteLine("{0}", Exception2);
						}
					});

					Main_Ended();
				}
#endif
			}
		}

		public void DumpThreads()
		{
			var ErrorOut = Console.Out;
			foreach (var Thread in HleThreadManager.Threads.ToArray())
			{
				ErrorOut.WriteLine("{0}", Thread);
				Thread.DumpStack(ErrorOut);
			}
			//throw new NotImplementedException();
		}
	}
}