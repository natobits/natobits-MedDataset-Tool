///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿

/*
	==> AUTO GENERATED FILE, edit ResamplingExtensions.tt instead <==
*/
namespace InnerEye.CreateDataset.Math
{
	using System;
	using InnerEye.CreateDataset.Volumes;

	public static class ResamplingExtensions
	{
	
        // Expects pixel coordinates.
        private static double Linear(this Volume3D<double> input, double pixelX, double pixelY, double pixelZ, double outsideValue)
        {
            int xx = (int)pixelX;
            int yy = (int)pixelY;
            int zz = (int)pixelZ;

            double x2 = pixelX - xx, y2 = pixelY - yy, z2 = pixelZ - zz;

            // local copies to help the compiler in subsequent optimizations
            var dimX = input.DimX;
            var dimY = input.DimY;
            var dimZ = input.DimZ;
            var dimXY = input.DimXY;

            // boundary check
            if (pixelX < 0 || pixelY < 0 || pixelZ < 0 || pixelX >= dimX - 1 || pixelY >= dimY - 1 || pixelZ >= dimZ - 1)
            {
                return input.LinearOutside(pixelX, pixelY, pixelZ, outsideValue);
            }

            // everything is inside
            int ind = xx + yy * dimX + zz * dimXY;
            var interpolation =
                  input[ind] * (1.0 - x2) * (1.0 - y2) * (1.0 - z2)
                + input[ind + 1] * x2 * (1.0 - y2) * (1.0 - z2)
                + input[ind + dimX] * (1.0 - x2) * y2 * (1.0 - z2)
                + input[ind + dimXY] * (1.0 - x2) * (1.0 - y2) * z2
                + input[ind + 1 + dimXY] * x2 * (1.0 - y2) * z2
                + input[ind + dimX + dimXY] * (1.0 - x2) * y2 * z2
                + input[ind + 1 + dimX] * x2 * y2 * (1.0 - z2)
                + input[ind + 1 + dimX + dimXY] * x2 * y2 * z2;
			return (double)interpolation;
        }

        private static void AdjustXyz(this Volume3D<double> input, double pixelX, double pixelY, double pixelZ,
            ref double x2, ref int xx,
            ref double y2, ref int yy,
            ref double z2, ref int zz)
        {
            if (pixelX < 0)
            {
                x2 = 0;
                xx = 0;
            }
            else if (pixelX > input.DimX - 1)
            {
                x2 = 0;
            }

            if (pixelY < 0)
            {
                y2 = 0;
                yy = 0;
            }
            else if (pixelY > input.DimY - 1)
            {
                y2 = 0;
            }

            if (pixelZ < 0)
            {
                z2 = 0;
                zz = 0;
            }
            else if (pixelZ > input.DimZ - 1)
            {
                z2 = 0;
            }
        }

		/// <summary>
		/// Creates a new volume of the given size, using linear interpolation to create the voxels in the new volume.
		/// </summary>
      