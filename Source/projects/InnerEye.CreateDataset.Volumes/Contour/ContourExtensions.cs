///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

namespace InnerEye.CreateDataset.Volumes
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Windows;
    
    using System.Diagnostics;

    [Obsolete("All contour-related code should move to using the new classes in the InnerEye.CreateDataset.Contours namespace.")]
    public static class ContourExtensions
    {
        [Obsolete("All contour-related code should move to using the new classes in the InnerEye.CreateDataset.Contours namespace.")]
        public static Volume2D<TK> AllocateSliceStorage<T, TK>(this Volume3D<T> volume, SliceType sliceType)
        {
            var width = 0;
            var height = 0;

            var spacingX = 0d;
            var spacingY = 0d;

            var origin = new Point2D();
            var direction = new Matrix2();

            switch (sliceType)
            {
                case SliceType.Axial:
                    width = volume.DimX;
                    height = volume.DimY;

                    spacingX = volume.SpacingX;
                    spacingY = volume.SpacingY;

                    if (volume.Origin.Data != null)
                    {
                        origin = new Point2D(volume.Origin.X, volume.Origin.Y);
                    }

                    if (volume.Direction.Data != null && volume.Direction.Data.Length == 9)
                    {
                        direction = new Matrix2(new[]
                        {
                            volume.Direction[0, 0],
                            volume.Direction[0, 1],
                            volume.Direction[1, 0],
                            volume.Direction[1, 1]
                        });
                    }

                    break;
                case SliceType.Coronal:
                    width = volume.DimX;
                    height = volume.DimZ;

                    spacingX = volume.SpacingX;
                    spacingY = volume.SpacingZ;

                    if (volume.Origin.Data != null)
                    {
                        origin = new Point2D(volume.Origin.X, volume.Origin.Z);
                    }

                    if (volume.Direction.Data != null && volume.Direction.Data.Length == 9)
                    {
                        direction = new Matrix2(new[]
                        {
                            volume.Direction[0, 0],
                            volume.Direction[0, 2],
                            volume.Direction[2, 0],
                            volume.Direction[2, 2]
                        });
                    }

                    break;
                case SliceType.Sagittal:
                    width = volume.DimY;
                    height = volume.DimZ;

                    spacingX = volume.SpacingY;
                    spacingY = volume.SpacingZ;

                    if (volume.Origin.Data != null)
                    {
                        origin = new Point2D(volume.Origin.Y, volume.Origin.Z);
                    }

                    if (volume.Direction.Data != null && volume.Direction.Data.Length == 9)
                    {
                        direction = new Matrix2(new[]
                        {
                            volume.Direction[1, 1],
                            volume.Direction[1, 2],
                            volume.Direction[2, 1],
                            volume.Direction[2, 2]
                        });
                    }

                    break;
            }

            return new Volume2D<TK>(width, height, spacingX, spacingY, origin, direction);
        }

        [Obsolete("All contour-related code should move to using the new classes in the InnerEye.CreateDataset.Contours namespace.")]
        public static void ExtractSlice<T>(this Volume3D<T> volume, SliceType sliceType, int sliceIndex, T[] outVolume, int skip = 1)
        {
            switch (sliceType)
            {
                case SliceType.Axial:
                    if (sliceIndex < volume.DimZ && outVolume.Length == volume.DimXY * skip)
                    {
                        Parallel.For(0, volume.DimY, delegate (int y)
                        {
                            for (var x = 0; x < volume.DimX; x++)
                            {
                                outVolume[(x + y * volume.DimX) * skip] = volume[((sliceIndex) * volume.DimY + y) * volume.DimX + x];
                            }
                        });
                    }
                    break;
                case SliceType.Coronal:
                    if (sliceIndex < volume.DimY && outVolume.Length == volume.DimZ * volume.DimX * skip)
                    {
                        Parallel.For(0, volume.DimZ, delegate (int z)
                        {
                            for (var x = 0; x < volume.DimX; x++)
                            {
                                outVolume[(x + z * volume.DimX) * skip] = volume[(z * volume.DimY + sliceIndex) * volume.DimX + x];
                            }
                        });
                    }
                    break;
                case SliceType.Sagittal:
                    if (sliceIndex < volume.DimX && outVolume.Length == volume.DimY * volume.DimZ * skip)
                    {
                        Parallel.For(0, volume.DimZ, delegate (int z)
                        {
                            for (var y = 0; y < volume.DimY; y++)
                            {
                                outVolume[(y + z * volume.DimY) * skip] = volume[(z * volume.DimY + y) * volume.DimX + sliceIndex];
                            }
                        });
                    }

                    break;
            }
        }

        [Obsolete("All contour-related code should move to using the new classes in the InnerEye.CreateDataset.Contours namespace.")]
        public static Volume2D<T> ExtractSlice<T>(this Volume3D<T> volume, SliceType sliceType, int index)
        {
            var result = volume.AllocateSliceStorage<T, T>(sliceType);

            if (result != null)
            {
                volume.ExtractSlice(sliceType, index, result.Array);
            }

            return result;
        }

        [Obsolete("All contour-related code should move to using the new classes in the InnerEye.CreateDataset.Contours namespace.")]
        public static ContoursBySlice ExtractContoursPerSlice(
            this Volume3D<byte> volume,
            byte fgId = ModelConstants.MaskForegroundIntensity,
            byte bgId = ModelConstants.MaskBackgroundIntensity,
            SliceType sliceType = SliceType.Axial,
            bool filterEmptyContours = true,
            Region3D<int> regionOfInterest = null,
            SmoothingType axialSmoothingType = SmoothingType.Small)
        {
            var region = regionOfInterest ?? new Region3D<int>(0, 0, 0, volume.DimX - 1, volume.DimY - 1, volume.DimZ - 1);

            int startPoint;
            int endPoint;

            // Only smooth the output on the axial slices
            var smoothingType = axialSmoothingType;

            switch (sliceType)
            {
                case SliceType.Axial:
                    startPoint = region.MinimumZ;
                    endPoint = region.MaximumZ;
                    break;
                case SliceType.Coronal:
                    startPoint = region.MinimumY;
                    endPoint = region.MaximumY;
                    smoothingType = SmoothingType.None;
                    break;
                case SliceType.Sagittal:
                    startPoint = region.MinimumX;
                    endPoint = region.MaximumX;
                    smoothingType = SmoothingType.None;
                    break;
                default:
                  