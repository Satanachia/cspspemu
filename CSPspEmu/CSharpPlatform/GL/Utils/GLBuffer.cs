﻿using System;
using System.Runtime.InteropServices;

namespace CSharpPlatform.GL.Utils
{
    public unsafe class GLBuffer : IDisposable
    {
        uint Buffer;

        private GLBuffer()
        {
            Initialize();
        }

        public static GLBuffer Create()
        {
            return new GLBuffer();
        }

        private void Initialize()
        {
            fixed (uint* BufferPtr = &Buffer)
            {
                GL.glGenBuffers(1, BufferPtr);
            }
        }

        public GLBuffer SetData<T>(T[] Data, int Offset = 0, int Length = -1)
        {
            if (Length < 0) Length = Data.Length;
            var Handle = GCHandle.Alloc(Data, GCHandleType.Pinned);
            try
            {
                return SetData(
                    Length * Marshal.SizeOf(typeof(T)),
                    ((byte*) Handle.AddrOfPinnedObject().ToPointer()) + Offset * Marshal.SizeOf(typeof(T))
                );
            }
            finally
            {
                Handle.Free();
            }
        }

        public GLBuffer SetData(int Size, void* Data)
        {
            Bind();
            GL.glBufferData(GL.GL_ARRAY_BUFFER, (uint) Size, Data, GL.GL_STATIC_DRAW);
            return this;
        }

        public void Bind()
        {
            GL.glBindBuffer(GL.GL_ARRAY_BUFFER, Buffer);
        }

        public void Unbind()
        {
            GL.glBindBuffer(GL.GL_ARRAY_BUFFER, 0);
        }

        public void Dispose()
        {
            fixed (uint* BufferPtr = &Buffer)
            {
                GL.glDeleteBuffers(1, BufferPtr);
            }
        }
    }
}