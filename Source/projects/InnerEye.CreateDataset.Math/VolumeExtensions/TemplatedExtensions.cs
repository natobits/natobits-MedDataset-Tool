///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿/*
	==> AUTO GENERATED FILE, edit CommonExtensions.tt instead <==
*/
namespace InnerEye.CreateDataset.Math
{
	using System;
    using System.Linq;
    using System.Threading.Tasks;
	using System.Collections.Generic;
	using InnerEye.CreateDataset.Volumes;

	public static partial class Converters
	{
	    /// <summary>
        /// Converts a floating point value to a byte value, using rounding. If the value is outside of the 
        /// valid range for byte, the returned value attains the minimum/maximum value for byte.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns></returns>
        public static byte ClampToByte(double value)
        {
            if (value < byte.MinValue)
            {
                return byte.MinValue;
            }

            if (value > byte.MaxValue)
            {
                return byte.MaxValue;
            }

            return (byte)Math.Round(value, MidpointRounding.AwayFromZero);
        }

	    /// <summary>
        /// Converts a floating point value to a byte value, using rounding. If the value is outside of the 
        /// valid range for byte, the returned value attains the minimum/maximum value for byte.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns></returns>
        public static byte ClampToByte(float value)
        {
            if (value < byte.MinValue)
            {
                return byte.MinValue;
            }

            if (value > byte.MaxValue)
            {
                return byte.MaxValue;
            }

            return (byte)Math.Round(value, MidpointRounding.AwayFromZero);
        }

	    /// <summary>
        /// Converts a floating point value to a short value, using rounding. If the value is outside of the 
        /// valid range for short, the returned value attains the minimum/maximum value for short.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns></returns>
        public static short ClampToInt16(double value)
        {
            if (value < short.MinValue)
            {
                return short.MinValue;
            }

            if (value > short.MaxValue)
            {
                return short.MaxValue;
            }

            return (short)Math.Round(value, MidpointRounding.AwayFromZero);
        }

	    /// <summary>
        /// Converts a floating point value to a short value, using rounding. If the value is outside of the 
        /// valid range for short, the returned value attains the minimum/maximum value for short.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns></returns>
        public static short ClampToInt16(float value)
        {
            if (value < short.MinValue)
            {
                return short.MinValue;
            }

            if (value > short.MaxValue)
            {
                return short.MaxValue;
            }

            return (short)Math.Round(value, MidpointRounding.AwayFromZero);
        }

	}

	public static class CommonExtensions
	{        /// <summary>
        /// Gets the region of the volume that contains all voxel values that are
		/// larger or equal than the interestId.
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="interestId">The voxel values to search for. Foreground is
		/// considered to be all voxels with a value larger or equal to the interestId.</param>
        /// <returns></returns>
		public static Region3D<int> GetInterestRegion(this Volume3D<int> volume, int interestId)
        {
            var minimumX = new int[volume.DimZ];
            var minimumY = new int[volume.DimZ];
            var minimumZ = new int[volume.DimZ];
            var maximumX = new int[volume.DimZ];
            var maximumY = new int[volume.DimZ];
            var maximumZ = new int[volume.DimZ];

            Parallel.For(0, volume.DimZ, del