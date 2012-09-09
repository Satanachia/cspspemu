﻿using System;
using System.IO;
using CSharpUtils;

namespace CSPspEmu.Core.Memory
{
	unsafe public class PspMemoryStream : Stream
	{
		protected uint _Position;
		public PspMemory Memory { get; protected set; }

		public PspMemoryStream(PspMemory Memory)
		{
			this.Memory = Memory;
		}

		public override bool CanRead { get { return true; } }
		public override bool CanSeek { get { return true; } }
		public override bool CanWrite { get { return true; } }

		public override void Flush()
		{
		}

		public override long Length
		{
			get { return unchecked(0xFFFFFFF0); }
		}

		public override long Position
		{
			get { return _Position; }
			set { _Position = (uint)value; }
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			switch (origin)
			{
				case SeekOrigin.Begin: Position = offset; break;
				case SeekOrigin.Current: Position = Position + offset; break;
				case SeekOrigin.End: Position = -offset; break;
			}
			return Position;
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			byte* Ptr = (byte*)Memory.PspAddressToPointerSafe(_Position, count);
			{
				PointerUtils.Memcpy(new ArraySegment<byte>(buffer, offset, count), Ptr);
			}
			_Position += (uint)count;
			return count;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			//Console.WriteLine("PspMemoryStream.Write(Size: {0}, _Position: 0x{1:X})", count, _Position);
			byte* Ptr = (byte*)Memory.PspAddressToPointerSafe(_Position, count);
			//Console.WriteLine("  Ptr: 0x{0:X}", (ulong)Ptr);
			{
				PointerUtils.Memcpy(Ptr, new ArraySegment<byte>(buffer, offset, count));
			}
			_Position += (uint)count;
		}
	}
}