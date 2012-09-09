﻿using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using AForge.Video.DirectShow.Internals;
using CSPspEmu.Core;
using CSharpUtils;

namespace CSPspEmu.Media
{
	public class OmaWavConverter
	{
		public static void convertOmaToWav(string Source, string Destination)
		{
			var Event = new AutoResetEvent(false);
			var Thread = new Thread(() =>
			{
				_convertOmaToWav(Source, Destination);
				Event.Set();
			});
			Thread.IsBackground = true;
			Thread.Start();
			Event.WaitOne(TimeSpan.FromSeconds(12));
			if (Thread.IsAlive) Thread.Abort();
		}

		static bool WarnOnce = false;

		private static void _convertOmaToWav(string Source, string Destination)
		{
			if (Platform.Is32Bit)
			{
				_convertOmaToWav_COM(Source, Destination);
			}
			else
			{
				var Folder = Directory.GetParent(Destination);
				var Oma2WavFile = String.Format("{0}/oma2wav.exe", Folder);
				if (!File.Exists(Oma2WavFile))
				{
					File.WriteAllBytes(Oma2WavFile, Assembly.GetEntryAssembly().GetManifestResourceStream("CSPspEmu.oma2wav.exe").ReadAll());
				}

				ProcessUtils.ExecuteCommand(Oma2WavFile, String.Format(@" ""{0}"" ""{1}"" ", Source, Destination));
			}
		}

		private static void _convertOmaToWav_COM(string Source, string Destination)
		{
			IGraphBuilder graphBuilder;
			IMediaControl mediaControl;
			IMediaEventEx mediaEvent;
			IBaseFilter sourceFilter;
			IBaseFilter omgTransform;
			IBaseFilter waveDest;
			IBaseFilter fileWriter;

			if (!File.Exists(Source)) throw (new Exception(String.Format("Can't find file '{0}'", Source)));

			graphBuilder = (IGraphBuilder)new FilgraphManager();
			mediaControl = (IMediaControl)graphBuilder;
			mediaEvent = (IMediaEventEx)graphBuilder;

			/*
			1. Open Registry Editor
			2. HKEY_LOCAL_MACHINE\SOFTWARE\Sony Corporation\OpenMG\
			3. InstallerVersion 5.4.00.04020
			*/
			try
			{
				sourceFilter = (IBaseFilter)new OpenMGOmgSourceFilter();
				omgTransform = (IBaseFilter)new OMG_TRANSFORM();
			}
			catch (COMException)
			{
				if (MessageBox.Show("Must download and install 'OpenMG Setup Update Program' in order to play Atrac3+ files\n\nGo to download page?", "Error", MessageBoxButtons.YesNo, MessageBoxIcon.Error) == DialogResult.Yes)
				{
					Process.Start("http://www.sony-mea.com/support/download/375063");
				}
				return;
			}

			Console.WriteLine("[1]");

			try
			{
				waveDest = (IBaseFilter)new WavDest();
			}
			catch (Exception Exception)
			{
				if (!WarnOnce)
				{
					WarnOnce = true;
					MessageBox.Show("Missing WavDest\r\n\r\n" + Exception, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
				}
				return;
			}

			DebugLine("[2]");

			fileWriter = (IBaseFilter)new File_writer();

			DebugLine("[3]");

			var fileSourceFilter = (IFileSourceFilter)sourceFilter;
			DebugLine(String.Format("fileSourceFilter.Load: {0}", fileSourceFilter.Load(Source, null)));

			DebugLine("[4]");

			var fileSinkFilter = (IFileSinkFilter)fileWriter;
			DebugLine(String.Format("fileSinkFilter.SetFileName: {0}", fileSinkFilter.SetFileName(Destination, null)));

			DebugLine("[5]");

			DebugLine(String.Format("graphBuilder.AddFilter(sourceFilter): {0}", graphBuilder.AddFilter(sourceFilter, "CLSID_OpenMGOmgSourceFilter")));
			DebugLine(String.Format("graphBuilder.AddFilter(omgTransform): {0}", graphBuilder.AddFilter(omgTransform, "CLSID_OMG_TRANSFORM")));
			DebugLine(String.Format("graphBuilder.AddFilter(waveDest): {0}", graphBuilder.AddFilter(waveDest, "CLSID_WavDest")));
			DebugLine(String.Format("graphBuilder.AddFilter(fileWriter): {0}", graphBuilder.AddFilter(fileWriter, "CLSID_File_writer")));
			IPin SourceOutPin;
			IPin FileWriterInPin;

			DebugLine("[6]");

			DebugLine(String.Format("sourceFilter.FindPin(Output): {0}", sourceFilter.FindPin("Output", out SourceOutPin)));
			DebugLine(String.Format("fileWriter.FindPin(in): {0}", fileWriter.FindPin("in", out FileWriterInPin)));

			DebugLine("[7]");

			DebugLine(String.Format("graphBuilder.Connect(SourceOutPin, FileWriterInPin): {0}", graphBuilder.Connect(SourceOutPin, FileWriterInPin)));

			DebugLine("[7a]");

			// Hang in some cases
			DebugLine(String.Format("graphBuilder.Render(SourceOutPin): {0}", graphBuilder.Render(SourceOutPin)));

			DebugLine("[7b]");

			DebugLine(String.Format("mediaControl.Run(SourceOutPin): {0}", mediaControl.Run()));

			DebugLine("[8]");

			int evCode;
			DebugLine("WaitForCompletion...");
			mediaEvent.WaitForCompletion(int.MaxValue, out evCode);
			DebugLine("WaitForCompletion... DONE");

			Action<object> Release = (o) =>
			{
				try
				{
					while (Marshal.ReleaseComObject(o) > 0) { }
				}
				catch
				{
				}
			};

			Release(sourceFilter);
			Release(omgTransform);
			Release(waveDest);
			Release(SourceOutPin);
			Release(FileWriterInPin);
			Release(mediaControl);
			Release(mediaEvent);
			Release(fileWriter);
			Release(fileSinkFilter);
			Release(graphBuilder);

			DebugLine("[9]");
		}

		private static void DebugLine(string Text)
		{
			Debug.WriteLine(Text);
		}
	}
}
