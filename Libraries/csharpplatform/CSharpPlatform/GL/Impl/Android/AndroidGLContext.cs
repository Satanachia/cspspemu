﻿using System;
using CSharpPlatform.GL.Impl.Windows;

namespace CSharpPlatform.GL.Impl.Android
{
    public class AndroidGLContext : IGlContext
    {
        private int Display;
        private IntPtr WindowHandle;

        public AndroidGLContext(IntPtr WindowHandle)
        {
            this.WindowHandle = WindowHandle;
            Display = EGL.eglGetDisplay(EGL.EGL_DEFAULT_DISPLAY);
            //EGL.eglCreateContext(Display);
            throw new NotImplementedException();
        }

        public static AndroidGLContext FromWindowHandle(IntPtr WindowHandle) => new AndroidGLContext(WindowHandle);
        public GlContextSize Size => throw new NotImplementedException();
        public IGlContext MakeCurrent() => throw new NotImplementedException();
        public IGlContext ReleaseCurrent() => throw new NotImplementedException();
        public IGlContext SwapBuffers() => throw new NotImplementedException();
        public void Dispose() => throw new NotImplementedException();
    }
}