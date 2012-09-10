﻿//#define LIST_SYNC

using System;
using System.Runtime.InteropServices;
using CSPspEmu.Core.Gpu;
using CSPspEmu.Core.Gpu.State;
using CSPspEmu.Core;
using CSPspEmu.Hle.Managers;

namespace CSPspEmu.Hle.Modules.ge
{
	public unsafe partial class sceGe_user
	{
		[Inject]
		public GpuProcessor GpuProcessor;

		[Inject]
		HleThreadManager ThreadManager;

		[Inject]
		HleMemoryManager MemoryManager;

		private GpuDisplayList GetDisplayListFromId(int DisplayListId) {
			return GpuProcessor.DisplayLists[DisplayListId];
		}

		MemoryPartition GpuStateStructPartition = null;
		GpuStateStruct* GpuStateStructPointer = null;

		public int _sceGeListEnQueue(uint InstructionAddressStart, uint InstructionAddressStall, int CallbackId, PspGeListArgs* Args, Action<GpuDisplayList> Action)
		{
			//Console.WriteLine("aaaaaaaaaaa");

			if (GpuStateStructPartition == null)
			{
				GpuStateStructPartition = MemoryManager.GetPartition(Managers.HleMemoryManager.Partitions.Kernel0).Allocate(
					sizeof(GpuStateStruct),
					Name: "GpuStateStruct"
				);
				GpuStateStructPointer = (GpuStateStruct*)MemoryManager.Memory.PspAddressToPointerSafe(GpuStateStructPartition.Low, Marshal.SizeOf(typeof(GpuStateStruct)));
			}

			//Console.WriteLine("_sceGeListEnQueue");
			try
			{
				var DisplayList = GpuProcessor.DequeueFreeDisplayList();
				{
					DisplayList.InstructionAddressStart = InstructionAddressStart;
					DisplayList.InstructionAddressCurrent = InstructionAddressStart;
					DisplayList.InstructionAddressStall = InstructionAddressStall;
					DisplayList.CallbacksId = -1;
					DisplayList.Callbacks = default(PspGeCallbackData);
					if (CallbackId != -1)
					{
						try
						{
							//DisplayList.Callbacks = Callbacks[CallbackId];
							DisplayList.Callbacks = Callbacks[CallbackId];
							DisplayList.CallbacksId = CallbackId;
						}
						catch
						{
						}
					}
					DisplayList.GpuStateStructPointer = null;
					if (Args != null)
					{
						DisplayList.GpuStateStructPointer = (GpuStateStruct*)CpuProcessor.Memory.PspAddressToPointerSafe(Args[0].GpuStateStructAddress, Marshal.SizeOf(typeof(GpuStateStruct)));
					}

					if (DisplayList.GpuStateStructPointer == null)
					{
						DisplayList.GpuStateStructPointer = GpuStateStructPointer;
					}
					Action(DisplayList);
				}
				return DisplayList.Id;
			}
			catch (Exception Exception)
			{
				Console.Error.WriteLine(Exception);
				//return -1;
				throw(Exception);
			}
		}

		/// <summary>
		/// Enqueue a display list at the tail of the GE display list queue.
		/// </summary>
		/// <param name="InstructionAddressStart">The head of the list to queue.</param>
		/// <param name="InstructionAddressStall">The stall address. If NULL then no stall address set and the list is transferred immediately.</param>
		/// <param name="CallbackId">ID of the callback set by calling <see cref="sceGeSetCallback"/></param>
		/// <param name="Args">Structure containing GE context buffer address</param>
		/// <returns>The DisplayList ID</returns>
		[HlePspFunction(NID = 0xAB49E76A, FirmwareVersion = 150)]
		[HlePspNotImplemented(PartialImplemented = true, Notice = false)]
		public int sceGeListEnQueue(uint InstructionAddressStart, uint InstructionAddressStall, int CallbackId, PspGeListArgs* Args)
		{
			return _sceGeListEnQueue(InstructionAddressStart, InstructionAddressStall, CallbackId, Args, (DisplayList) =>
			{
				GpuProcessor.EnqueueDisplayListLast(DisplayList);
#if LIST_SYNC
				DisplayList.WaitCompletedSync();
#endif
			});
		}

		/// <summary>
		/// Enqueue a display list at the head of the GE display list queue.
		/// </summary>
		/// <param name="InstructionAddressStart">The head of the list to queue.</param>
		/// <param name="InstructionAddressStall">The stall address. If NULL then no stall address set and the list is transferred immediately.</param>
		/// <param name="CallbackId">ID of the callback set by calling <see cref="sceGeSetCallback"/></param>
		/// <param name="Args">Structure containing GE context buffer address</param>
		/// <returns>The DisplayList ID</returns>
		[HlePspFunction(NID = 0x1C0D95A6, FirmwareVersion = 150)]
		[HlePspNotImplemented(PartialImplemented = true, Notice = false)]
		public int sceGeListEnQueueHead(uint InstructionAddressStart, uint InstructionAddressStall, int CallbackId, PspGeListArgs* Args)
		{
			return _sceGeListEnQueue(InstructionAddressStart, InstructionAddressStall, CallbackId, Args, (DisplayList) =>
			{
				GpuProcessor.EnqueueDisplayListFirst(DisplayList);
#if LIST_SYNC
				DisplayList.WaitCompletedSync();
#endif
			});
		}

		/// <summary>
		/// Cancel a queued or running list.
		/// </summary>
		/// <param name="DisplayListId">A DisplayList ID</param>
		/// <returns>???</returns>
		[HlePspFunction(NID = 0x5FB86AB0, FirmwareVersion = 150)]
		[HlePspNotImplemented(PartialImplemented = true)]
		public int sceGeListDeQueue(int DisplayListId)
		{
			var DisplayList = GetDisplayListFromId(DisplayListId);
			GpuProcessor.DisplayListQueue.Remove(DisplayList);
			return 0;
		}

		/// <summary>
		/// Update the stall address for the specified queue.
		/// </summary>
		/// <param name="DisplayListId">The ID of the queue.</param>
		/// <param name="InstructionAddressStall">The stall address to update</param>
		/// <returns>Unknown. Probably 0 if successful.</returns>
		[HlePspFunction(NID = 0xE0D68148, FirmwareVersion = 150)]
		public int sceGeListUpdateStallAddr(int DisplayListId, uint InstructionAddressStall)
		{
			var DisplayList = GetDisplayListFromId(DisplayListId);
			DisplayList.InstructionAddressStall = InstructionAddressStall;
			return 0;
		}

		/// <summary>
		/// Wait for syncronisation of a list.
		/// </summary>
		/// <param name="DisplayListId">The queue ID of the list to sync.</param>
		/// <param name="SyncType">Specifies the condition to wait on.  One of PspGeSyncType.</param>
		/// <returns>???</returns>
		[HlePspFunction(NID = 0x03444EB4, FirmwareVersion = 150)]
		//[HlePspNotImplemented]
		public int sceGeListSync(int DisplayListId, GpuProcessor.SyncTypeEnum SyncType)
		{
			//return 0;
			//Console.WriteLine("sceGeListSync:{0},{1}", DisplayListId, SyncType);

			var DisplayList = GetDisplayListFromId(DisplayListId);

			ThreadManager.Current.SetWaitAndPrepareWakeUp(HleThread.WaitType.GraphicEngine, "sceGeListSync", DisplayList, (WakeUpCallbackDelegate) =>
			{
				DisplayList.GeListSync(SyncType, () =>
				{
					WakeUpCallbackDelegate();
				});
			});

			return 0;
		}

		/// <summary>
		/// Wait for drawing to complete.
		/// </summary>
		/// <param name="SyncType">Specifies the condition to wait on.  One of ::PspGeSyncType.</param>
		/// <returns>???</returns>
		[HlePspFunction(NID = 0xB287BD61, FirmwareVersion = 150, CheckInsideInterrupt = true)]
		[HlePspNotImplemented(PartialImplemented = true, Notice = false)]
		public int sceGeDrawSync(GpuProcessor.SyncTypeEnum SyncType)
		{
			//return 0;
			//return 0;

			//Console.WriteLine("sceGeDrawSync:{0}", SyncType);

			var CurrentThread = ThreadManager.Current;

			if (CurrentThread == null)
			{
				Console.Error.WriteLine("sceGeDrawSync.CurrentThread == null");
				return -1;
			}

			CurrentThread.SetWaitAndPrepareWakeUp(HleThread.WaitType.GraphicEngine, "sceGeDrawSync", null, (WakeUpCallbackDelegate) =>
			{
				GpuProcessor.GeDrawSync(SyncType, () =>
				{
					WakeUpCallbackDelegate();
				});
			});

			return 0;
		}

		/// <summary>
		/// 
		/// </summary>
		public struct PspGeListArgs
		{
			/// <summary>
			/// Size
			/// </summary>
			public uint Size;

			/// <summary>
			/// Pointer to a GpuStateStruct
			/// </summary>
			public uint GpuStateStructAddress;
		}
	}
}
