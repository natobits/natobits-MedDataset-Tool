///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace MedLib.IO.Extensions
{
	using System;
    using System.IO;
    using static MedLib.IO.NiftiIO;

	/// <summary>
	/// Static methods to convert arrays of {byte, short, float, UInt16} encoded as byte arrays to {byte, short, float, UInt16} applying a linear map to values
	/// as they are processed. 
	/// </summary>
    public static class VolumeRescaleConvert
	{

		/// <summary>
		/// Returns an action to convert an array of bytes encoding a contiguous array of type byte to an array of type byte applying slope and intercept
		/// to the given values. All values are clamped to the range byte.MinValue and byte.MaxValue as appropriate.
		/// </summary>
		public unsafe static Action<int, int> Convertbyte(byte[] srcBytes, byte[] output, float slope, float intercept)
		{
			return (startIndex, endIndex) =>
			{
				fixed (byte* pSrc = srcBytes)
				fixed (byte* pDest = output)
				{
					byte* pDestEnd = pDest + endIndex;
					byte* pDestPtr = pDest + startIndex;
#pragma warning disable IDE0004 // Remove Unnecessary Cast
					byte* pSrcPtr = (byte*)pSrc + startIndex;
#pragma warning restore IDE0004 // Remove Unnecessary Cast
					for (; pDestPtr <= pDestEnd; pDestPtr++, pSrcPtr++)
					{
						var v = *pSrcPtr * slope + intercept;
						int vi = (int)Math.Round(v);
						*pDestPtr =(byte)(vi < byte.MinValue ? byte.MinValue : (vi > byte.MaxValue ? byte.MaxValue : vi));
					}
				}
			};
		}

        /// <summary>
        /// Returns an action to convert an array of bytes encoding a contiguous array of type short to an array of type byte applying slope and intercept
        /// to the given values. All values are clamped to the range byte.MinValue and byte.MaxValue as appropriate.
        /// </summary>
        public unsafe static Action<int, int> Convertshort(byte[] srcBytes, byte[] output, float slope, float intercept)
		{
			return (startIndex, endIndex) =>
			{
				fixed (byte* pSrc = srcBytes)
				fixed (byte* pDest = output)
				{
					byte* pDestEnd = pDest + endIndex;
					byte* pDestPtr = pDest + startIndex;
#pragma warning disable IDE0004 // Remove Unnecessary Cast
					short* pSrcPtr = (short*)pSrc + startIndex;
#pragma warning restore IDE0004 // Remove Unnecessary Cast
					for (; pDestPtr <= pDestEnd; pDestPtr++, pSrcPtr++)
					{
						var v = *pSrcPtr * slope + intercept;
						int vi = (int)Math.Round(v);
						*pDestPtr =(byte)(vi < byte.MinValue ? byte.MinValue : (vi > byte.MaxValue ? byte.MaxValue : vi));
					}
				}
			};
		}

        /// <summary>
        /// Returns an action to convert an array of bytes encoding a contiguous array of type ushort to an array of type byte applying slope and intercept
        /// to the given values. All values are clamped to the range byte.MinValue and byte.MaxValue as appropriate.
        /// </summary>
        public unsafe static Action<int, int> Convertushort(byte[] srcBytes, byte[] output, float slope, float intercept)
		{
			return (startIndex, endIndex) =>
			{
				fixed (byte* pSrc = srcBytes)
				fixed (byte* pDest = output)
				{
					byte* pDestEnd = pDest + endIndex;
					byte* pDestPtr = pDest + startIndex;
#pragma warning disable IDE0004 // Remove Unnecessary Cast
					ushort* pSrcPtr = (ushort*)pSrc + startIndex;
#pragma warning restore IDE0004 // Remove Unnecessary Cast
					for (; pDestPtr <= pDestEnd; pDestPtr++, pSrcPtr++)
					{
						var v = *pSrcPtr * slope + intercept;
						int vi = (int)Math.Round(v);
						*pDestPtr =(byte)(vi < byte.MinValue ? byte.MinValue : (vi > byte.MaxValue ? byte.MaxValue : vi));
					}
				}
			};
		}

		/// <summary>
		/// Returns an action to convert an array of bytes encoding a contiguous array of type float to an array of type byte applying slope and intercept
		/// to the given values. All values are clamped to the range byte.MinValue and byte.MaxValue as appropriate.
		/// </summary>
		public unsafe static Action<int, int> Convertfloat(byte[] srcBytes, byte[] output, float slope, float intercept)
		{
			return (startIndex, endIndex) =>
			{
				fixed (byte* pSrc = srcBytes)
				fixed (byte* pDest = output)
				{
					byte* pDestEnd = pDest + endIndex;
					byte* pDestPtr = pDest + startIndex;
#pragma warning disable IDE0004 // Remove Unnecessary Cast
					float* pSrcPtr = (float*)pSrc + startIndex;
#pragma warning restore IDE0004 // Remove Unnecessary Cast
					for (; pDestPtr <= pDestEnd; pDestPtr++, pSrcPtr++)
					{
						var v = *pSrcPtr * slope + intercept;
						int vi = (int)Math.Round(v);
						*pDestPtr =(byte)(vi < byte.MinValue ? byte.MinValue : (vi > byte.MaxValue ? byte.MaxValue : vi));
					}
				}
			};
		}

		/// <summary>
		/// Returns an action to convert an array of bytes encoding a contiguous array of type byte to an array of type short applying slope and intercept
		/// to the given values. All values are clamped to the range short.MinValue and short.MaxValue as appropriate.
		/// </summary>
		public unsafe static Action<int, int> Convertbyte(byte[] srcBytes, short[] output, float slope, float intercept)
		{
			return (startIndex, endIndex) =>
			{
				fixed (byte* pSrc = srcBytes)
				fixed (short* pDest = output)
				{
					short* pDestEnd = pDest + endIndex;
					short* pDestPtr = pDest + startIndex;
					byte* pSrcPtr = pSrc + startIndex;
					for (; pDestPtr <= pDestEnd; pDestPtr++, pSrcPtr++)
					{
						var v = *pSrcPtr * slope + intercept;
						int vi = (int)Math.Round(v);
						*pDestPtr =(short)(vi < short.MinValue ? short.MinValue : (vi > short.MaxValue ? short.MaxValue : vi));
					}
				}
			};
		}

		
		/// <summary>
		/// Returns an action to convert an array of bytes encoding a contiguous array of type short to an array of type short applying slope and intercept
		/// to the given values. All values are clamped to the range short.MinValue and short.MaxValue as appropriate.
		/// </summary>
		public unsafe static Action<int, int> Convertshort(byte[] srcBytes, short[] output, float slope, float intercept)
		{
			return (startIndex, endIndex) =>
			{
				fixed (byte* pSrc = srcBytes)
				fixed (short* pDest = output)
				{
					short* pDestEnd = pDest + endIndex;
					short* pDestPtr = pDest + startIndex;
#pragma warning disable IDE0004 // Remove Unnecessary Cast
					short* pSrcPtr = (short*)pSrc + startIndex;
#pragma warning restore IDE0004 // Remove Unnecessary Cast
					for (; pDestPtr <= pDestEnd; pDestPtr++, pSrcPtr++)
					{
						var v = *pSrcPtr * slope + intercept;
						int vi = (int)Math.Round(v);
						*pDestPtr =(short)(vi < short.MinValue ? short.MinValue : (vi > short.MaxValue ? short.MaxValue : vi));
					}
				}
			};
		}

		/// <summary>
		/// Returns an action to convert an array of bytes encoding a contiguous array of type ushort to an array of type short applying slope and intercept
		/// to the given values. All values are clamped to the range short.MinValue and short.MaxValue as appropriate.
		/// </summary>
		public unsafe static Action<int, int> Convertushort(byte[] srcBytes, short[] output, float slope, float intercept)
		{
			return (startIndex, endIndex) =>
			{
				fixed (byte* pSrc = srcBytes)
				fixed (short* pDest = output)
				{
					short* pDestEnd = pDest + endIndex;
					short* pDestPtr = pDest + startIndex;
#pragma warning disable IDE0004 // Remove Unnecessary Cast
					ushort* pSrcPtr = (ushort*)pSrc + startIndex;
#pragma warning restore IDE0004 // Remove Unnecessary Cast
					for (; pDestPtr <= pDestEnd; pDestPtr++, pSrcPtr++)
					{
						var v = *pSrcPtr * slope + intercept;
						int vi = (int)Math.Round(v);
						*pDestPtr =(short)(vi < short.MinValue ? short.MinValue : (vi > short.MaxValue ? short.MaxValue : vi));
					}
				}
			};
		}

		/// <summary>
		/// Returns an action to convert an array of bytes encoding a contiguous array of type float to an array of type short applying slope and intercept
		/// to the given values. All values are clamped to the range short.MinValue and short.MaxValue as appropriate.
		/// </summary>
		public unsafe static Action<int, int> Convertfloat(byte[] srcBytes, short[] output, float slope, float intercept)
		{
			return (startIndex, endIndex) =>
			{
				fixed (byte* pSrc = srcBytes)
				fixed (short* pDest = output)
				{
					short* pDestEnd = pDest + endIndex;
					short* pDestPtr = pDest + startIndex;
#pragma warning disable IDE0004 // Remove Unnecessary Cast
					float* pSrcPtr = (float*)pSrc + startIndex;
#pragma warning restore IDE0004 // Remove Unnecessary Cast
					for (; pDestPtr <= pDestEnd; pDestPtr++, pSrcPtr++)
					{
						var v = *pSrcPtr * slope + intercept;
						int vi = (int)Math.Round(v);
						*pDestPtr =(short)(vi < short.MinValue ? short.MinValue : (vi > short.MaxValue ? short.MaxValue : vi));
					}
				}
			};
		}

        /// <summary>
        /// Returns an action to convert an array of bytes encoding a contiguous array of type byte to an array of type ushort applying slope and intercept
        /// to the given values. All values are clamped to the range ushort.MinValue and ushort.MaxValue as appropriate.
        /// </summary>
        public unsafe static Action<int, int> Convertbyte(byte[] srcBytes, ushort[] output, float slope, float intercept)
		{
			return (startIndex, endIndex) =>
			{
				fixed (byte* pSrc = srcBytes)
				fixed (ushort* pDest = output)
				{
					ushort* pDestEnd = pDest + endIndex;
					ushort* pDestPtr = pDest + startIndex;
#pragma warning disable IDE0004 // Remove Unnecessary Cast
					byte* pSrcPtr = (byte*)pSrc + startIndex;
#pragma warning restore IDE0004 // Remove Unnecessary Cast
					for (; pDestPtr <= pDestEnd; pDestPtr++, pSrcPtr++)
					{
						var v = *pSrcPtr * slope + intercept;
						int vi = (int)Math.Round(v);
						*pDestPtr =(ushort)(vi < ushort.MinValue ? ushort.MinValue : (vi > ushort.MaxValue ? ushort.MaxValue : vi));
					}
				}
			};
		}

		/// <summary>
		/// Returns an action to convert an array of bytes encoding a contiguous array of type short to an array of type ushort applying slope and intercept
		/// to the given values. All values are clamped to the range ushort.MinValue and ushort.MaxValue as appropriate.
		/// </summary>
		public unsafe static Action<int, int> Convertshort(byte[] srcBytes, ushort[] output, float slope, float intercept)
		{
			return (startIndex, endIndex) =>
			{
				fixed (byte* pSrc = srcBytes)
				fixed (ushort* pDest = output)
				{
					ushort* pDestEnd = pDest + endIndex;
					ushort* pDestPtr = pDest + startIndex;
#pragma warning disable IDE0004 // Remove Unnecessary Cast
					short* pSrcPtr = (short*)pSrc + startIndex;
#pragma warning restore IDE0004 // Remove Unnecessary Cast
					for (; pDestPtr <= pDestEnd; pDestPtr++, pSrcPtr++)
					{
						var v = *pSrcPtr * slope + intercept;
						int vi = (int)Math.Round(v);
						*pDestPtr =(ushort)(vi < ushort.MinValue ? ushort.MinValue : (vi > ushort.MaxValue ? ushort.MaxValue : vi));
					}
				}
			};
		}

		/// <summary>
		/// Returns an action to convert an array of bytes encoding a contiguous array of type ushort to an array of type ushort applying slope and intercept
		/// to the given values. All values are clamped to the range ushort.MinValue and ushort.MaxValue as appropriate.
		/// </summary>
		public unsafe static Action<int, int> Convertushort(byte[] srcBytes, ushort[] output, float slope, float intercept)
		{
			return (startIndex, endIndex) =>
			{
				fixed (byte* pSrc = srcBytes)
				fixed (ushort* pDest = output)
				{
					ushort* pDestEnd = pDest + endIndex;
					ushort* pDestPtr = pDest + startIndex;
#pragma warning disable IDE0004 // Remove Unnecessary Cast
					ushort* pSrcPtr = (ushort*)pSrc + startIndex;
#pragma warning restore IDE0004 // Remove Unnecessary Cast
					for (; pDestPtr <= pDestEnd; pDestPtr++, pSrcPtr++)
					{
						var v = *pSrcPtr * slope + intercept;
						int vi = (int)Math.Round(v);
						*pDestPtr =(ushort)(vi < ushort.MinValue ? ushort.MinValue : (vi > ushort.MaxValue ? ushort.MaxValue : vi));
					}
				}
			};
		}

		/// <summary>
		/// Returns an action to convert an array of bytes encoding a contiguous array of type float to an array of type ushort applying slope and intercept
		/// to the given values. All values are clamped to the range ushort.MinValue and ushort.MaxValue as appropriate.
		/// </summary>
		public unsafe static Action<int, int> Convertfloat(byte[] srcBytes, ushort[] output, float slope, float intercept)
		{
			return (startIndex, endIndex) =>
			{
				fixed (byte* pSrc = srcBytes)
				fixed (ushort* pDest = output)
				{
					ushort* pDestEnd = pDest + endIndex;
					ushort* pDestPtr = pDest + startIndex;
#pragma warning disable IDE0004 // Remove Unnecessary Cast
					float* pSrcPtr = (float*)pSrc + startIndex;
#pragma warning restore IDE0004 // Remove Unnecessary Cast
					for (; pDestPtr <= pDestEnd; pDestPtr++, pSrcPtr++)
					{
						var v = *pSrcPtr * slope + intercept;
						int vi = (int)Math.Round(v);
						*pDestPtr =(ushort)(vi < ushort.MinValue ? ushort.MinValue : (vi > ushort.MaxValue ? ushort.MaxValue : vi));
					}
				}
			};
		}

		/// <summary>
		/// Returns an action to convert an array of bytes encoding a contiguous array of type byte to an array of type float applying slope and intercept
		/// to the given values. All values are clamped to the range float.MinValue and float.MaxValue as appropriate.
		/// </summary>
		public unsafe static Action<int, int> Convertbyte(byte[] srcBytes, float[] output, float slope, float intercept)
		{
			return (startIndex, endIndex) =>
			{
				fixed (byte* pSrc = srcBytes)
				fixed (float* pDest = output)
				{
					float* pDestEnd = pDest + endIndex;
					float* pDestPtr = pDest + startIndex;
#pragma warning disable IDE0004 // Remove Unnecessary Cast
					byte* pSrcPtr = (byte*)pSrc + startIndex;
#pragma warning restore IDE0004 // Remove Unnecessary Cast
					for (; pDestPtr <= pDestEnd; pDestPtr++, pSrcPtr++)
					{
						var v = *pSrcPtr * slope + intercept;
						float vi = (v);
						*pDestPtr =vi;
					}
				}
			};
		}

		/// <summary>
		/// Returns an action to convert an array of bytes encoding a contiguous array of type short to an array of type float applying slope and intercept
		/// to the given values. All values are clamped to the range float.MinValue and float.MaxValue as appropriate.
		/// </summary>
		public unsafe static Action<int, int> Convertshort(byte[] srcBytes, float[] output, float slope, float intercept)
		{
			return (startIndex, endIndex) =>
			{
				fixed (byte* pSrc = srcBytes)
				fixed (float* pDest = output)
				{
					float* pDestEnd = pDest + endIndex;
					float* pDestPtr = pDest + startIndex;
#pragma warning disable IDE0004 // Remove Unnecessary Cast
					short* pSrcPtr = (short*)pSrc + startIndex;
#pragma warning restore IDE0004 // Remove Unnecessary Cast
					for (; pDestPtr <= pDestEnd; pDestPtr++, pSrcPtr++)
					{
						var v = *pSrcPtr * slope + intercept;
						float vi = (v);
						*pDestPtr =vi;
					}
				}
			};
		}

		/// <summary>
		/// Returns an action to convert an array of bytes encoding a contiguous array of type ushort to an array of type float applying slope and intercept
		/// to the given values. All values are clamped to the range float.MinValue and float.MaxValue as appropriate.
		/// </summary>
		public unsafe static Action<int, int> Convertushort(byte[] srcBytes, float[] output, float slope, float intercept)
		{
			return (startIndex, endIndex) =>
			{
				fixed (byte* pSrc = srcBytes)
				fixed (float* pDest = output)
				{
					float* pDestEnd = pDest + endIndex;
					float* pDestPtr = pDest + startIndex;
#pragma warning disable IDE0004 // Remove Unnecessary Cast