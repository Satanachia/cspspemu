﻿using System.Threading;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CSharpUtilsTests
{
	[TestClass]
	public class ReaderWriterLockExtensionsTest
	{
		[TestMethod]
		public void ReaderLockTest()
		{
			var ReaderWriterLock  = new ReaderWriterLock();
			var StartEvent = new CountdownEvent(2);
			var StartedEvent = new CountdownEvent(2);
			var EndEvent = new CountdownEvent(2);
			new Thread(() =>
			{
				StartEvent.Signal(1);
				StartEvent.Wait();
				StartedEvent.Signal(1);
				ReaderWriterLock.ReaderLock(() =>
				{
					Thread.Sleep(60);
				});
				EndEvent.Signal(1);
			}).Start();
			new Thread(() =>
			{
				StartEvent.Signal(1);
				StartEvent.Wait();
				StartedEvent.Signal(1);
				ReaderWriterLock.ReaderLock(() =>
				{
					Thread.Sleep(60);
				});
				EndEvent.Signal(1);
			}).Start();
			StartedEvent.Wait();

			var TestStopwatch = new Stopwatch();
			TestStopwatch.Start();
			EndEvent.Wait();
			TestStopwatch.Stop();

			Assert.IsTrue(TestStopwatch.ElapsedMilliseconds < 110);
		}

		[TestMethod]
		public void WriterLockTest()
		{
			var ReaderWriterLock = new ReaderWriterLock();
			var StartEvent = new CountdownEvent(2);
			var StartedEvent = new CountdownEvent(2);
			var EndEvent = new CountdownEvent(2);
			new Thread(() =>
			{
				StartEvent.Signal(1);
				StartEvent.Wait();
				StartedEvent.Signal(1);
				ReaderWriterLock.WriterLock(() =>
				{
					Thread.Sleep(60);
				});
				EndEvent.Signal(1);
			}).Start();
			new Thread(() =>
			{
				StartEvent.Signal(1);
				StartEvent.Wait();
				StartedEvent.Signal(1);
				ReaderWriterLock.ReaderLock(() =>
				{
					Thread.Sleep(60);
				});
				EndEvent.Signal(1);
			}).Start();
			StartedEvent.Wait();

			var TestStopwatch = new Stopwatch();
			TestStopwatch.Start();
			EndEvent.Wait();
			TestStopwatch.Stop();

			Assert.IsTrue(TestStopwatch.ElapsedMilliseconds > 110);
		}

	}
}
