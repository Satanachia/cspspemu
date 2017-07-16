﻿using CSharpUtils.Streams;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CSharpUtilsTests.Streams
{
    /// <summary>
    /// 
    /// </summary>
    [TestClass]
    public class ZeroStreamTest
    {
        /// <summary>
        /// 
        /// </summary>
        [TestMethod]
        public void TestRead()
        {
            var Stream = new ZeroStream(7, 0x11);
            byte[] Read1, Read2;
            Read1 = Stream.ReadBytesUpTo(3);
            CollectionAssert.AreEqual(new byte[] { 0x11, 0x11, 0x11 }, Read1);
            Read2 = Stream.ReadBytesUpTo(7);
            CollectionAssert.AreEqual(new byte[] { 0x11, 0x11, 0x11, 0x11 }, Read2);
        }
    }
}
