﻿using System;
using OpenTK;
using OpenTK.Audio.OpenAL;

namespace CSPspEmu.Core.Audio.Impl.Openal
{
	/// <summary>
	/// 48 Samples 1 ms
	/// </summary>
	internal sealed class AudioStream
	{
		public const int Frequency = 44100;
		//public const int Frequency = 48000;

		//public const double SamplesPerMillisecond = ((double)Frequency) / 1000;
		public const double SamplesPerMillisecond = ((double)Frequency) / 500;
		public const int NumberOfBuffers = 4;
		public const int NumberOfChannels = 2;
		public const int BufferMilliseconds = 10;
		public const int SamplesPerBuffer = (int)(SamplesPerMillisecond * BufferMilliseconds * NumberOfChannels);
		public int[] BufferIds;
		public int SourceId;

		static void ALEnforce(string AT = "Unknown")
		{
			var Error = AL.GetError();
			if (Error != ALError.NoError)
			{
				Console.Error.WriteLine("ALEnforce: " + AL.GetErrorString(Error) + "(" + Error + ") : " + AT);
				//throw (new Exception("Error: " + AL.GetErrorString(Error)));
			}
		}

		static T ALEnforce<T>(T Input)
		{
			ALEnforce();
			return Input;
		}

		public AudioStream()
		{
			this.BufferIds = ALEnforce(AL.GenBuffers(NumberOfBuffers));
			this.SourceId = ALEnforce(AL.GenSource());

			var Position = new Vector3(0, 0, 0);
			var Velocity = new Vector3(0, 0, 0);

			AL.Source(SourceId, ALSourcef.Pitch, 1.0f); ALEnforce();
			AL.Source(SourceId, ALSourcef.Gain, 1.0f); ALEnforce();
			AL.Source(SourceId, ALSourcef.RolloffFactor, 0.0f); ALEnforce();
			//AL.Source(SourceId, ALSourceb.Looping, true); ALEnforce();
			AL.Source(SourceId, ALSource3f.Velocity, ref Velocity); ALEnforce();
			AL.Source(SourceId, ALSource3f.Position, ref Position); ALEnforce();
		}

		~AudioStream()
		{
			AL.DeleteSource(this.SourceId); ALEnforce();
			AL.DeleteBuffers(this.BufferIds); ALEnforce();
		}

		private void Start()
		{
			foreach (var BufferId in BufferIds)
			{
				ReadStream(BufferId);
				AL.SourceQueueBuffer(SourceId, BufferId); ALEnforce();
			}
			AL.SourcePlay(SourceId); ALEnforce();
		}

		public bool IsPlaying
		{
			get
			{
				int state = 0;
				AL.GetSource(SourceId, ALGetSourcei.SourceState, out state);
				return (state == (int)ALSourceState.Playing);
			}
		}

		public void Update(Action<short[]> ReadStreamCallback)
		{
			int Processed = -1;

			if (!IsPlaying)
			{
				AL.DeleteSource(this.SourceId); ALEnforce();
				this.SourceId = ALEnforce(AL.GenSource());
				//AL.SourceStop(SourceId);
				Start();
				//AL.SourcePlay(SourceId); ALEnforce();
			}

			AL.GetSource(SourceId, ALGetSourcei.BuffersProcessed, out Processed);
			ALEnforce();

			while (Processed-- > 0)
			{
				int DequeuedBufferId = ALEnforce(AL.SourceUnqueueBuffer(SourceId));
				{
					ReadStream(DequeuedBufferId, ReadStreamCallback);
				}
				AL.SourceQueueBuffer(SourceId, DequeuedBufferId); ALEnforce();
			}
		}

		short[] BufferData = null;
		private void ReadStream(int BufferId, Action<short[]> ReadStreamCallback = null)
		{
			//short[] BufferData;

			int ReadSamples = SamplesPerBuffer;
			if (BufferData == null || BufferData.Length != ReadSamples)
			{
				BufferData = new short[ReadSamples];
				//Console.WriteLine("Created buffer");
			}

			if (ReadStreamCallback != null)
			{
				ReadStreamCallback(BufferData);
				//if (BufferData.Any(Item => Item != 0)) foreach (var C in BufferData) Console.Write("{0},", C);
			}
			else
			{
				//BufferData = new short[ReadSamples];
			}

			AL.BufferData(BufferId, ALFormat.Stereo16, BufferData, BufferData.Length * sizeof(short), Frequency); ALEnforce("ReadStream");
		}
	}
}
