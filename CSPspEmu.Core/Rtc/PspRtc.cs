﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using CSharpUtils.Extensions;

namespace CSPspEmu.Core.Rtc
{
	public class PspRtc : PspEmulatorComponent
	{
		public class VirtualTimer
		{
			protected PspRtc PspRtc;

			protected DateTime _DateTime;

			public DateTime DateTime
			{
				set
				{
					lock (PspRtc.Timers)
					{
						lock (this)
						{
							this._DateTime = value;
							if (!OnList)
							{
								PspRtc.Timers.AddLast(this);
								OnList = true;
							}
						}
					}
				}
				get
				{
					return _DateTime;
				}
			}
			public bool OnList;

			internal Action Callback;
			public bool Enabled;

			internal VirtualTimer(PspRtc PspRtc)
			{
				this.PspRtc = PspRtc;
			}

			public void SetIn(TimeSpan TimeSpan)
			{
				this.DateTime = DateTime.UtcNow + TimeSpan;
			}

			public void SetAt(DateTime DateTime)
			{
				this.DateTime = DateTime;
			}

			public override string ToString()
			{
				return this.ToStringDefault();
			}
		}

		protected LinkedList<VirtualTimer> Timers = new LinkedList<VirtualTimer>();
		public DateTime StartDateTime;
		public DateTime CurrentDateTime;
		public TimeSpan Elapsed
		{
			get {
				return CurrentDateTime - StartDateTime;
			}
		}

		public DateTime UpdatedCurrentDateTime
		{
			get
			{
				Update();
				return CurrentDateTime;
			}
		}

		public uint UnixTimeStamp
		{
			get
			{
				return (uint)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;
			}
		}

		public override void InitializeComponent()
		{
			Start();
		}

		public void Start()
		{
			this.StartDateTime = DateTime.UtcNow;
		}

		public void Update()
		{
			this.CurrentDateTime = DateTime.UtcNow;

			lock (Timers)
			{
			RetryLoop:
				foreach (var Timer in Timers)
				{
					lock (Timer)
					{
						//Console.Error.WriteLine(Timer);
						if (Timer.Enabled && this.CurrentDateTime >= Timer.DateTime)
						{
							//Console.Error.WriteLine("Tick!");
							Timers.Remove(Timer);
							Timer.Callback();
							Timer.OnList = false;
							goto RetryLoop;
						}
					}
				}
			}
		}

		public VirtualTimer CreateVirtualTimer(Action Callback)
		{
			return new VirtualTimer(this)
			{
				Callback = Callback,
			};
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="TimeSpan"></param>
		/// <param name="Callback"></param>
		public VirtualTimer RegisterTimerInOnce(TimeSpan TimeSpan, Action Callback)
		{
			//Console.WriteLine("Time: " + TimeSpan);
			return RegisterTimerAtOnce(DateTime.UtcNow + TimeSpan, Callback);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="DateTime"></param>
		/// <param name="Callback"></param>
		public VirtualTimer RegisterTimerAtOnce(DateTime DateTime, Action Callback)
		{
			lock (Timers)
			{
				//Console.WriteLine("RegisterTimerAtOnce:" + DateTime);
				var VirtualTimer = CreateVirtualTimer(Callback);
				VirtualTimer.SetAt(DateTime);
				VirtualTimer.Enabled = true;
				return VirtualTimer;
			}
		}
	}
}
