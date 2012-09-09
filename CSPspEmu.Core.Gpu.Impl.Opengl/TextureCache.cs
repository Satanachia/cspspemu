﻿//#define DEBUG_TEXTURE_CACHE

using System;
using System.Collections.Generic;
using System.Drawing;
using CSharpUtils;
using CSPspEmu.Core.Gpu.State.SubStates;
using CSPspEmu.Core.Memory;
using CSPspEmu.Core.Utils;
using CSPspEmu.Core.Gpu.State;

#if OPENTK
using OpenTK.Graphics.OpenGL;
#else
using MiniGL;
#endif

namespace CSPspEmu.Core.Gpu.Impl.Opengl
{
	public sealed unsafe class Texture : IDisposable
	{
		public int TextureId { get; private set; }
		public ulong TextureHash
		{
			get
			{
				return TextureCacheKey.TextureHash;
			}
		}

		public OpenglGpuImpl OpenglGpuImpl;
		public DateTime RecheckTimestamp;
		public TextureCacheKey TextureCacheKey;
		public int Width;
		public int Height;

		public Texture(OpenglGpuImpl OpenglGpuImpl)
		{
			this.OpenglGpuImpl = OpenglGpuImpl;
			OpenglGpuImpl.GraphicsContext.MakeCurrent(OpenglGpuImpl.WindowInfo);

			//lock (OpenglGpuImpl.GpuLock)
			{
				TextureId = GL.GenTexture();
				var GlError = GL.GetError();
				//Console.Error.WriteLine("GenTexture: {0} : Thread : {1} <- {2}", GlError, Thread.CurrentThread.ManagedThreadId, TextureId);
				if (GlError != ErrorCode.NoError)
				{
					//TextureId = 0;
				}
			}
		}

		public void Save(string File)
		{
			var Bitmap = new Bitmap(this.Width, this.Height);
			fixed (OutputPixel* DataPtr = Data)
			{
				BitmapUtils.TransferChannelsDataInterleaved(
					Bitmap.GetFullRectangle(),
					Bitmap,
					(byte*)DataPtr,
					BitmapUtils.Direction.FromDataToBitmap,
					BitmapChannel.Red,
					BitmapChannel.Green,
					BitmapChannel.Blue,
					BitmapChannel.Alpha
				);
			}
			Bitmap.Save(File);
		}

		OutputPixel[] Data;

		public Texture Load(string FileName)
		{
			var Bitmap = new Bitmap(Image.FromFile(FileName));
			Bitmap.LockBitsUnlock(System.Drawing.Imaging.PixelFormat.Format32bppArgb, (BitmapData) =>
			{
				this.SetData((OutputPixel *)BitmapData.Scan0, BitmapData.Width, BitmapData.Height);
			});
			return this;
		}

		public bool SetData(OutputPixel *Pixels, int TextureWidth, int TextureHeight)
		{
			//lock (OpenglGpuImpl.GpuLock)
			{
				//if (TextureId != 0)
				{
					this.Width = TextureWidth;
					this.Height = TextureHeight;

					Data = new OutputPixel[TextureWidth * TextureHeight];
					fixed (OutputPixel* DataPtr = Data)
					{
						PointerUtils.Memcpy((byte*)DataPtr, (byte*)Pixels, TextureWidth * TextureHeight * sizeof(OutputPixel));
					}

					Bind();
					GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, TextureWidth, TextureHeight, 0, PixelFormat.Rgba, PixelType.UnsignedInt8888Reversed, new IntPtr(Pixels));
					var GlError = GL.GetError();
					GL.Flush();

					if (GlError != ErrorCode.NoError)
					{
						Console.Error.WriteLine("########## ERROR: TexImage2D: {0} : TexId:{1} : {2} : {3}x{4}", GlError, TextureId, new IntPtr(Pixels), TextureWidth, TextureHeight);
						TextureId = 0;
						Bind();
						return false;
					}
				}
			}
			return true;

			//glTexEnvf(GL_TEXTURE_ENV, GL_RGB_SCALE, 1.0); // 2.0 in scale_2x
			//GL.TexEnv(TextureEnvTarget.TextureEnv, GL_TEXTURE_ENV_MODE, TextureEnvModeTranslate[state.texture.effect]);
		}

		public void Bind()
		{
			//lock (OpenglGpuImpl.GpuLock)
			{
				if (TextureId != 0)
				{
					//GL.Enable(EnableCap.Texture2D);
					GL.BindTexture(TextureTarget.Texture2D, TextureId);

					var GlError = GL.GetError();
					if (GlError != ErrorCode.NoError)
					{
						//Console.Error.WriteLine("Bind: {0} : {1}", GlError, TextureId);
					}
				}
				else
				{
					//GL.Disable(EnableCap.Texture2D);
				}
			}
		}

		public void Dispose()
		{
			if (TextureId != 0)
			{
				GL.DeleteTexture(TextureId);
				TextureId = 0;
			}
		}

		public override string ToString()
		{
			return this.ToStringDefault();
		}
	}

	public struct TextureCacheKey
	{
		public uint TextureAddress;
		public ulong TextureHash;
		public GuPixelFormats TextureFormat;

		public uint ClutAddress;
		public ulong ClutHash;
		public GuPixelFormats ClutFormat;
		public int ClutStart;
		public int ClutShift;
		public int ClutMask;
		public bool Swizzled;
		public bool ColorTestEnabled;
		public OutputPixel ColorTestRef;
		public OutputPixel ColorTestMask;
		public ColorTestFunctionEnum ColorTestFunction;

		public override string ToString()
		{
			return this.ToStringDefault();
		}
	}

	public sealed unsafe class TextureCache : PspEmulatorComponent
	{
		[Inject]
		PspMemory PspMemory;
		//Dictionary<TextureCacheKey, Texture> Cache = new Dictionary<TextureCacheKey, Texture>();
		Dictionary<ulong, Texture> Cache = new Dictionary<ulong, Texture>();
		public OpenglGpuImpl OpenglGpuImpl;

		byte[] SwizzlingBuffer = new byte[4 * 1024 * 1024];
		OutputPixel[] DecodedTextureBuffer = new OutputPixel[1024 * 1024];

		public override void InitializeComponent()
		{
		}

		Texture InvalidTexture;

		public Texture Get(GpuStateStruct *GpuState)
		{
			var TextureMappingState = &GpuState->TextureMappingState;
			var ClutState = &TextureMappingState->ClutState;
			var TextureState = &TextureMappingState->TextureState;

			Texture Texture;
			//GC.Collect();
			bool Swizzled = TextureState->Swizzled;
			uint TextureAddress = TextureState->Mipmap0.Address;
			uint ClutAddress = ClutState->Address;
			var ClutFormat = ClutState->PixelFormat;
			var ClutStart = ClutState->Start;
			var ClutDataStart = PixelFormatDecoder.GetPixelsSize(ClutFormat, ClutStart);

			ulong Hash1 = TextureAddress | (ulong)((ClutAddress + ClutDataStart) << 32);
			bool Recheck = false;
			if (Cache.TryGetValue(Hash1, out Texture))
			{
				if (Texture.RecheckTimestamp != RecheckTimestamp)
				{
					Recheck = true;
				}
			}
			else
			{
				Recheck = true;
			}

			if (Recheck)
			{
				//Console.Write(".");

				//Console.WriteLine("{0:X}", ClutAddress);
				var TexturePointer = (byte*)PspMemory.PspAddressToPointerSafe(TextureAddress);
				var ClutPointer = (byte *)PspMemory.PspAddressToPointerSafe(ClutAddress);
				var TextureFormat = TextureState->PixelFormat;
				//var Width = TextureState->Mipmap0.TextureWidth;

				int BufferWidth = TextureState->Mipmap0.BufferWidth;

				// FAKE!
				//BufferWidth = TextureState->Mipmap0.TextureWidth;

				var Height = TextureState->Mipmap0.TextureHeight;
				var TextureDataSize = PixelFormatDecoder.GetPixelsSize(TextureFormat, BufferWidth * Height);
				if (ClutState->NumberOfColors > 256)
				{
					ClutState->NumberOfColors = 256;
				}
				var ClutDataSize = PixelFormatDecoder.GetPixelsSize(ClutFormat, ClutState->NumberOfColors);
				var ClutCount = ClutState->NumberOfColors;
				var ClutShift = ClutState->Shift;
				var ClutMask = ClutState->Mask;

				//Console.WriteLine(TextureFormat);


				if (!PspMemory.IsRangeValid(TextureAddress, TextureDataSize) || TextureDataSize > 2048 * 2048 * 4)
				{
					Console.Error.WriteLine("UPDATE_TEXTURE(TEX={0},CLUT={1}:{2}:{3}:{4}:0x{5:X},SIZE={6}x{7},{8},Swizzled={9})", TextureFormat, ClutFormat, ClutCount, ClutStart, ClutShift, ClutMask, BufferWidth, Height, BufferWidth, Swizzled);
					Console.Error.WriteLine("Invalid TEXTURE! TextureAddress=0x{0:X}, TextureDataSize={1}", TextureAddress, TextureDataSize);
					Console.Error.WriteLine("Invalid TEXTURE!");
					if (InvalidTexture == null)
					{
						InvalidTexture = new Texture(OpenglGpuImpl);

						int ITWidth = 2, ITHeight = 2;
						int ITWidthHeight = ITWidth * ITHeight;
						fixed (OutputPixel* Data = new OutputPixel[ITWidthHeight])
						{
							var Color1 = OutputPixel.FromRGBA(0xFF, 0x00, 0x00, 0xFF);
							var Color2 = OutputPixel.FromRGBA(0x00, 0x00, 0xFF, 0xFF);
							for (int n = 0; n < ITWidthHeight; n++)
							{
								Data[n] = ((n & 1) != 0) ? Color1 : Color2;
							}
							InvalidTexture.SetData(Data, ITWidth, ITHeight);
						}
					}
					return InvalidTexture;
				}

				//Console.WriteLine("TextureAddress=0x{0:X}, TextureDataSize=0x{1:X}", TextureAddress, TextureDataSize);

				TextureCacheKey TextureCacheKey = new TextureCacheKey()
				{
					TextureAddress = TextureAddress,
					TextureFormat = TextureFormat,
					TextureHash = FastHash(TexturePointer, TextureDataSize),

					ClutHash = FastHash(&(ClutPointer[ClutDataStart]), ClutDataSize),
					ClutAddress = ClutAddress,
					ClutFormat = ClutFormat,
					ClutStart = ClutStart,
					ClutShift = ClutShift,
					ClutMask = ClutMask,
					Swizzled = Swizzled,

					ColorTestEnabled = GpuState->ColorTestState.Enabled,
					ColorTestRef = GpuState->ColorTestState.Ref,
					ColorTestMask = GpuState->ColorTestState.Mask,
					ColorTestFunction = GpuState->ColorTestState.Function,
				};

				if (Texture == null || (!Texture.TextureCacheKey.Equals(TextureCacheKey)))
				{
					string TextureName = "texture_" + TextureCacheKey.TextureHash + "_" + TextureCacheKey.ClutHash + "_" + TextureFormat + "_" + ClutFormat + "_" + BufferWidth + "x" + Height + "_" + Swizzled;
#if DEBUG_TEXTURE_CACHE

					Console.Error.WriteLine("UPDATE_TEXTURE(TEX={0},CLUT={1}:{2}:{3}:{4}:0x{5:X},SIZE={6}x{7},{8},Swizzled={9})", TextureFormat, ClutFormat, ClutCount, ClutStart, ClutShift, ClutMask, BufferWidth, Height, BufferWidth, Swizzled);
#endif
					Texture = new Texture(OpenglGpuImpl);
					Texture.TextureCacheKey = TextureCacheKey;
					{
						//int TextureWidth = Math.Max(BufferWidth, Height);
						//int TextureHeight = Math.Max(BufferWidth, Height);
						int TextureWidth = BufferWidth;
						int TextureHeight = Height;
						int TextureWidthHeight = TextureWidth * TextureHeight;

						fixed (OutputPixel* TexturePixelsPointer = DecodedTextureBuffer)
						{
							if (Swizzled)
							{
								fixed (byte* SwizzlingBufferPointer = SwizzlingBuffer)
								{
									PointerUtils.Memcpy(SwizzlingBuffer, TexturePointer, TextureDataSize);
									PixelFormatDecoder.UnswizzleInline(TextureFormat, (void*)SwizzlingBufferPointer, BufferWidth, Height);
									PixelFormatDecoder.Decode(
										TextureFormat, (void*)SwizzlingBufferPointer, TexturePixelsPointer, BufferWidth, Height,
										ClutPointer, ClutFormat, ClutCount, ClutStart, ClutShift, ClutMask, StrideWidth: PixelFormatDecoder.GetPixelsSize(TextureFormat, TextureWidth)
									);
								}
							}
							else
							{
								PixelFormatDecoder.Decode(
									TextureFormat, (void*)TexturePointer, TexturePixelsPointer, BufferWidth, Height,
									ClutPointer, ClutFormat, ClutCount, ClutStart, ClutShift, ClutMask, StrideWidth: PixelFormatDecoder.GetPixelsSize(TextureFormat, TextureWidth)
								);
							}

							if (TextureCacheKey.ColorTestEnabled)
							{
								byte EqualValue, NotEqualValue;

								switch (TextureCacheKey.ColorTestFunction)
								{
									case ColorTestFunctionEnum.GU_ALWAYS: EqualValue = 0xFF; NotEqualValue = 0xFF; break;
									case ColorTestFunctionEnum.GU_NEVER: EqualValue = 0x00; NotEqualValue = 0x00; break;
									case ColorTestFunctionEnum.GU_EQUAL: EqualValue = 0xFF; NotEqualValue = 0x00; break;
									case ColorTestFunctionEnum.GU_NOTEQUAL: EqualValue = 0x00; NotEqualValue = 0xFF; break;
									default: throw(new NotImplementedException());
								}

								ConsoleUtils.SaveRestoreConsoleState(() =>
								{
									Console.BackgroundColor = ConsoleColor.Red;
									Console.ForegroundColor = ConsoleColor.Yellow;
									Console.Error.WriteLine("{0} : {1}, {2} : ref:{3} : mask:{4}", TextureCacheKey.ColorTestFunction, EqualValue, NotEqualValue, TextureCacheKey.ColorTestRef, TextureCacheKey.ColorTestMask);
								});

								for (int n = 0; n < TextureWidthHeight; n++)
								{
									if ((TexturePixelsPointer[n] & TextureCacheKey.ColorTestMask).Equals((TextureCacheKey.ColorTestRef & TextureCacheKey.ColorTestMask)))
									{
										TexturePixelsPointer[n].A = EqualValue;
									}
									else
									{
										TexturePixelsPointer[n].A = NotEqualValue;
									}
									if (TexturePixelsPointer[n].A == 0)
									{
										//Console.Write("yup!");
									}
								}
							}
							
#if DEBUG_TEXTURE_CACHE
							var Bitmap = new Bitmap(BufferWidth, Height);
							BitmapUtils.TransferChannelsDataInterleaved(
								Bitmap.GetFullRectangle(),
								Bitmap,
								(byte*)TexturePixelsPointer,
								BitmapUtils.Direction.FromDataToBitmap,
								BitmapChannel.Red,
								BitmapChannel.Green,
								BitmapChannel.Blue,
								BitmapChannel.Alpha
							);
							Bitmap.Save(TextureName + ".png");
#endif

							var Result = Texture.SetData(TexturePixelsPointer, TextureWidth, TextureHeight);
#if DEBUG_TEXTURE_CACHE
							if (!Result || Texture.TextureId == 0)
							{
								var Bitmap2 = new Bitmap(BufferWidth, Height);
								BitmapUtils.TransferChannelsDataInterleaved(
									Bitmap2.GetFullRectangle(),
									Bitmap2,
									(byte*)TexturePixelsPointer,
									BitmapUtils.Direction.FromDataToBitmap,
									BitmapChannel.Red,
									BitmapChannel.Green,
									BitmapChannel.Blue,
									BitmapChannel.Alpha
								);
								string TextureName2 = @"C:\projects\csharp\cspspemu\__invalid_texture_" + TextureCacheKey.TextureHash + "_" + TextureCacheKey.ClutHash + "_" + TextureFormat + "_" + ClutFormat + "_" + BufferWidth + "x" + Height;
								Bitmap.Save(TextureName2 + ".png");
							}
#endif
						}
					}
					if (Cache.ContainsKey(Hash1))
					{
						Cache[Hash1].Dispose();
					}
					Cache[Hash1] = Texture;
				}
			}

			Texture.RecheckTimestamp = RecheckTimestamp;

			return Texture;
		}

		private DateTime RecheckTimestamp = DateTime.MinValue;

		public void RecheckAll()
		{
			RecheckTimestamp = DateTime.UtcNow;
		}

		public static ulong FastHash(byte* Pointer, int Count, ulong StartHash = 0)
		{
			return Hashing.FastHash(Pointer, Count, StartHash);
		}
	}
}
