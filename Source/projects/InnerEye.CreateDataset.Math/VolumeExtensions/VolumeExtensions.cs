
///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace InnerEye.CreateDataset.Math
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Drawing.Imaging;
    using System.IO;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Runtime.InteropServices;
    using System.Threading.Tasks;
    using MedLib.IO;
    using InnerEye.CreateDataset.Contours;
    using InnerEye.CreateDataset.ImageProcessing;
    using InnerEye.CreateDataset.Volumes;

    public static partial class Converters
    {
        /// <summary>
        /// Converts a Float32 value to an Int16 value, if it is in the correct range for Int16, and if
        /// they appear to contain integer values after rounding to 5 digits. An input value of 1.0000001 would be considered
        /// OK, and converted to (short)1. If the value is outside the Int16 range, or appears to be a fractional
        /// value, throws an <see cref="ArgumentException"/>.
        /// </summary>
        /// <param name="value"></param>
        /// <param name="index">The index at which the value was found. This is only used for creating error messages.</param>
        /// <returns></returns>
        public static short TryConvertToInt16(float value, int index = -1)
        {
            if (value < short.MinValue || value > short.MaxValue)
            {
                var position = index >= 0 ? $"at index { index}" : string.Empty;
                throw new ArgumentException($"The input image contains voxel values outside the range of Int16: Found value {value} {position}");
            }
            var rounded = Math.Round(value, 5);
            if (rounded != (short)rounded)
            {
                var position = index >= 0 ? $"at index { index}" : string.Empty;
                throw new ArgumentException($"The input image contains voxel values that are not integers after rounding to 5 decimals: Found value {value} {position}");
            }
            return (short)rounded;
        }

        /// <summary>
        /// Converts a probability with values between 0 and 1.0, to a byte with values between 0 and 255.
        /// An input voxel value of 1.0 would be mapped to 255 in the output.
        /// Any values at or below 0.0 would become 0, anything exceeding 1.0 in the input will become 255.
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        public static byte PosteriorToByte(float value)
        {
            var val = value * 255.0;
            return (val <= 0) ? (byte)0 : (val >= 255) ? (byte)255 : (byte)Math.Round(val);
        }
    }

    public static class VolumeExtensions
    {
        /// <summary>
        /// Gets the X,Y,Z location coordinates for a given index positions
        /// </summary>
        public static Index3D GetCoordinates<T>(this Volume3D<T> image, int index)
        {
            var z = index / image.DimXY;
            var rem = index - z * image.DimXY;
            var y = rem / image.DimX;
            var x = rem - y * image.DimX;
            return new Index3D((x, y, z), index);
        }

        /// <summary>
        /// Gets the sequence of voxel with their values in an image, that fall inside the mask (that is, the 
        /// mask value that corresponds to the voxels is not equal to zero). Returns all voxel values
        /// if the mask is null.
        /// </summary>
        /// <param name="image">The image to process.</param>
        /// <param name="mask">The binary mask that specifies which voxels of the image should be returned.
        /// Can be null.</param>
        /// <returns></returns>
        public static IEnumerable<T> VoxelsInsideMask<T>(this Volume3D<T> image, Volume3D<byte> mask)
        {
            var imageArray = (image ?? throw new ArgumentNullException(nameof(image))).Array;
            var maskArray = mask?.Array;
            return
                maskArray != null
                ? VoxelsInsideMaskWhenImageAndMaskAreNotNull(imageArray, maskArray)
                : image.Array;
        }

        /// <summary>
        /// Gets the sequence of voxel with their values and X,Y,Z location in an image, that fall inside the mask (that is, the 
        /// mask value that corresponds to the voxels is not equal to zero). Returns all voxel values
        /// if the mask is null.
        /// </summary>
        /// <param name="image">The image to process.</param>
        /// <param name="mask">The binary mask that specifies which voxels of the image should be returned.
        /// Can be null.</param>
        /// <returns></returns>
        public static IEnumerable<(Index3D coordinates, T value)> VoxelsInsideMaskWithCoordinates<T>(this Volume3D<T> image, Volume3D<byte> mask)
        {
            var imageArray = (image ?? throw new ArgumentNullException(nameof(image))).Array;
            if (mask == null)
            {
                for (var index = 0; index < imageArray.Length; index++)
                {
                    yield return (image.GetCoordinates(index), imageArray[index]);
                }
            }
            else
            {
                var imageLength = imageArray.Length;
                var maskArray = mask.Array;
                if (maskArray.Length != imageLength)
                {
                    throw new ArgumentException("The image and the mask must have the same number of voxels.", nameof(mask));
                }

                // Plain vanilla loop is the fastest way of iterating, 4x faster than Array.Indices()
                for (var index = 0; index < imageLength; index++)
                {
                    if (maskArray[index] > 0)
                    {
                        yield return (image.GetCoordinates(index), imageArray[index]);
                    }
                }
            }
        }

        /// <summary>
        /// Converts the given volume to Nifti format, using the given compression method.
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="compression"></param>
        /// <returns></returns>
        public static byte[] SerializeToNiftiBytes(this Volume3D<short> volume, NiftiCompression compression)
        {
            using (var memoryStream = new MemoryStream())
            {
                NiftiIO.WriteToStream(memoryStream, volume, compression);
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Converts the given volume to Nifti format, using the given compression method.
        /// </summary>
        /// <param name="volume"></param>
        /// <param name="compression"></param>
        /// <returns></returns>
        public static byte[] SerializeToNiftiBytes(this Volume3D<byte> volume, NiftiCompression compression)
        {
            using (var memoryStream = new MemoryStream())
            {
                NiftiIO.WriteToStream(memoryStream, volume, compression);
                return memoryStream.ToArray();
            }
        }

        /// <summary>
        /// Helper method to iterate slice wize in parallel ie: ((z,y,x) tuples over the volume dimensions) iterate over a volume and execute an method
        /// </summary>
        /// <param name="volume">The volume to be iterated</param>
        /// <param name="action">The action to be invoked upon each iteration</param>
        /// <param name="options">options to control parallelisation</param>
        /// <returns></returns>
        public static void ParallelIterateSlices<T>(this Volume3D<T> volume, Action<(int x, int y, int z)> action, ParallelOptions options = null)
        {
            Parallel.For(0, volume.DimZ, options ?? new ParallelOptions(), z =>
            {
                for (var y = 0; y < volume.DimY; y++)
                {
                    for (var x = 0; x < volume.DimX; x++)
                    {
                        action((x, y, z));
                    }
                }
            });
        }

        /// <summary>
        /// Helper method to iterate slice wize ie: ((z,y,x) tuples over the volume dimensions) iterate over a volume and execute an method
        /// </summary>
        /// <param name="volume">The volume to be iterated</param>
        /// <param name="action">The action to be invoked upon each iteration</param>
        /// <returns></returns>
        public static void IterateSlices<T>(this Volume3D<T> volume, Action<(int x, int y, int z)> action)
        {
            ParallelIterateSlices(volume, action, new ParallelOptions { MaxDegreeOfParallelism = 1 });
        }

        /// <summary>
        /// Creates a Volume3D instance from individual slices (XY planes).
        /// This is intended for testing only: The resulting Volume3D will have
        /// information like Origin set to their default values.
        /// </summary>
        /// <param name="dimX">The DimX property of the returned object.</param>
        /// <param name="dimY">The DimY property of the returned object.</param>
        /// <param name="slices">A list of individual slices. slices[i] will be used as z=i.</param>
        /// <returns></returns>
        public static Volume3D<T> FromSlices<T>(int dimX, int dimY, IReadOnlyList<IReadOnlyList<T>> slices)
        {
            var expectedSize = dimX * dimY;
            if (slices == null || slices.Count == 0)
            {
                throw new ArgumentException("Slice list must not be empty", nameof(slices));
            }
            if (slices.Any(slice => slice.Count != expectedSize))
            {
                throw new ArgumentException($"Each slice must match dimensions and have length {expectedSize}", nameof(slices));
            }
            var m = new Volume3D<T>(dimX, dimY, slices.Count);
            for (var z = 0; z < slices.Count; z++)
            {
                Array.Copy(slices[z].ToArray(), 0, m.Array, z * m.DimXY, m.DimXY);
            }
            return m;
        }

        public static IEnumerable<byte> GetAllValuesInVolume(this Volume3D<byte> volume)
        {
            var values = new int[256];

            for (var i = 0; i < volume.Length; i++)
            {
                values[volume[i]]++;
            }

            var result = new List<byte>();

            // Start at 1 as 0 is background
            for (var i = 1; i < values.Length; i++)
            {
                if (values[i] > 0)
                {
                    result.Add((byte)i);
                }
            }

            return result;
        }

        private static float[] GetSigmasForConvolution<T>(this Volume3D<T> image, float sigma)
        {
            return new float[] {
                (float)(sigma / image.SpacingX),
                (float)(sigma / image.SpacingY),
                (float)(sigma / image.SpacingZ)};
        }

        private static Direction[] GetDirectionsForConvolution()
        {
            return new Direction[] { Direction.DirectionX, Direction.DirectionY, Direction.DirectionZ };
        }

        /// <summary>
        /// Runs Gaussian smoothing on the given volume in-place.
        /// </summary>
        /// <param name="image"></param>
        /// <param name="sigma"></param>
        public static void SmoothInPlace(this Volume3D<byte> image, float sigma)
        {
            Convolution.Convolve(image.Array, image.DimX, image.DimY, image.DimZ,
                GetDirectionsForConvolution(), image.GetSigmasForConvolution(sigma));
        }

        /// <summary>
        /// Runs Gaussian smoothing on the given volume in-place.
        /// </summary>
        /// <param name="image"></param>
        /// <param name="sigma"></param>
        public static void SmoothInPlace(this Volume3D<float> image, float sigma)
        {
            Convolution.Convolve(image.Array, image.DimX, image.DimY, image.DimZ,
                GetDirectionsForConvolution(), image.GetSigmasForConvolution(sigma));
        }

        public static Volume3D<byte> SmoothedImage(this Volume3D<byte> image, double sigma)
        {
            var output = image.Copy();
            Convolution.Convolve(output.Array, output.DimX, output.DimY, output.DimZ,
                GetDirectionsForConvolution(), output.GetSigmasForConvolution((float)sigma));
            return output;
        }

        public static Volume3D<float> SmoothedImage(this Volume3D<float> image, double sigma)
        {
            var output = image.Copy();
            Convolution.Convolve(output.Array, output.DimX, output.DimY, output.DimZ,
                GetDirectionsForConvolution(), output.GetSigmasForConvolution((float)sigma));
            return output;
        }

        /// <summary>
        /// Gets the region of the volume that contains all non-zero voxel values.
        /// </summary>
        /// <param name="volume"></param>
        /// <returns></returns>
        public static Region3D<int> GetInterestRegion(this Volume3D<byte> volume)
        {
            var minimumX = new int[volume.DimZ];
            var minimumY = new int[volume.DimZ];
            var minimumZ = new int[volume.DimZ];
            var maximumX = new int[volume.DimZ];
            var maximumY = new int[volume.DimZ];
            var maximumZ = new int[volume.DimZ];

            Parallel.For(0, volume.DimZ, delegate (int z)
            {
                minimumX[z] = int.MaxValue;
                minimumY[z] = int.MaxValue;
                minimumZ[z] = int.MaxValue;
                maximumX[z] = -int.MaxValue;
                maximumY[z] = -int.MaxValue;
                maximumZ[z] = -int.MaxValue;

                for (var y = 0; y < volume.DimY; y++)
                {
                    for (var x = 0; x < volume.DimX; x++)
                    {
                        if (volume[x + y * volume.DimX + z * volume.DimXY] <= 0)
                        {
                            continue;
                        }

                        if (x < minimumX[z])
                        {
                            minimumX[z] = x;
                        }

                        if (x > maximumX[z])
                        {
                            maximumX[z] = x;
                        }

                        if (y < minimumY[z])
                        {
                            minimumY[z] = y;
                        }

                        if (y > maximumY[z])
                        {
                            maximumY[z] = y;
                        }

                        if (z < minimumZ[z])
                        {
                            minimumZ[z] = z;
                        }

                        maximumZ[z] = z;
                    }
                }
            });

            var region = new Region3D<int>(minimumX.Min(), minimumY.Min(), minimumZ.Min(), maximumX.Max(), maximumY.Max(), maximumZ.Max());

            // If no foreground values are found, the region minimum will be Int.MaxValue, maximum will be Int.MinValue.
            // When accidentally doing operations on that region, it will most likely lead to numerical
            // overflow or underflow. Instead, return an empty region that has less troublesome boundary values.
            return region.IsEmpty() ? RegionExtensions.EmptyIntRegion() : region;
        }

        public static Image ToImage(this Volume2D<byte> volume)
        {
            var result = new Bitmap(volume.DimX, volume.DimY);

            for (var y = 0; y < volume.DimY; y++)
            {
                for (var x = 0; x < volume.DimX; x++)
                {
                    var colorValue = volume[x, y] == 0 ? 255 : 0;
                    result.SetPixel(x, y, Color.FromArgb(colorValue, colorValue, colorValue));
                }
            }

            return result;
        }

        public static Image ToImage(this Volume2D<float> volume)
        {
            var result = new Bitmap(volume.DimX, volume.DimY);

            for (var y = 0; y < volume.DimY; y++)
            {
                for (var x = 0; x < volume.DimX; x++)
                {
                    var colorValue = (int)volume[x, y] == 0 ? 255 : 0;
                    result.SetPixel(x, y, Color.FromArgb(colorValue, colorValue, colorValue));
                }
            }

            return result;
        }

        public static Image ToImage(this Volume2D<double> volume)
        {
            var result = new Bitmap(volume.DimX, volume.DimY);

            for (var y = 0; y < volume.DimY; y++)
            {
                for (var x = 0; x < volume.DimX; x++)
                {
                    var colorValue = (int)volume[x, y] == 0 ? 255 : 0;
                    result.SetPixel(x, y, Color.FromArgb(colorValue, colorValue, colorValue));
                }
            }

            return result;
        }

        public static Volume2D<byte> ToVolume(this Bitmap image)
        {
            return new Volume2D<byte>(image.ToByteArray(), image.Width, image.Height, 1, 1, new Point2D(), Matrix2.CreateIdentity());
        }

        /// <summary>
        /// Creates a PNG image file from the given volume. Specific voxel values in the volume are mapped
        /// to fixed colors in the PNG file, as per the given mapping.
        /// </summary>
        /// <param name="mask"></param>
        /// <param name="filePath"></param>
        /// <param name="voxelMapping"></param>
        public static void SaveVolumeToPng(this Volume2D<byte> mask, string filePath,
            IDictionary<byte, Color> voxelMapping,
            Color? defaultColor = null)
        {
            var width = mask.DimX;
            var height = mask.DimY;

            var image = new Bitmap(width, height);

            CreateFolderStructureIfNotExists(filePath);
            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var maskValue = mask[x, y];
                    if (!voxelMapping.TryGetValue(maskValue, out var color))
                    {
                        color = defaultColor ?? throw new ArgumentException($"The voxel-to-color mapping does not contain an entry for value {maskValue} found at point ({x}, {y}), and no default color is set.", nameof(voxelMapping));
                    }

                    image.SetPixel(x, y, color);
                }
            }

            image.Save(filePath);
        }

        /// <summary>
        /// Creates a PNG image file from the given binary mask. Value 0 is plotted as white, 
        /// value 1 as black. If the mask contains other values, an exception is thrown.
        /// </summary>
        /// <param name="brushVolume"></param>
        /// <param name="filePath"></param>
        public static void SaveBinaryMaskToPng(this Volume2D<byte> mask, string filePath)
        {
            var voxelMapping = new Dictionary<byte, Color>
            {
                {0, Color.White },
                {1, Color.Black }
            };
            SaveVolumeToPng(mask, filePath, voxelMapping);
        }

        /// <summary>
        /// Creates a PNG image file from the given volume. Specific voxel values in the volume are mapped
        /// to fixed colors in the PNG file:
        /// Background (value 0) is plotted in Red
        /// Foreground (value 1) is Green
        /// Value 2 is Orange
        /// Value 3 is MediumAquamarine
        /// All other voxel values are plotted in Blue.
        /// </summary>
        /// <param name="brushVolume"></param>
        /// <param name="filePath"></param>
        public static void SaveBrushVolumeToPng(this Volume2D<byte> brushVolume, string filePath)
        {
            const byte fg = 1;
            const byte bg = 0;
            const byte bfg = 3;
            const byte bbg = 2;
            var voxelMapping = new Dictionary<byte, Color>
            {
                { fg, Color.Green },
                { bfg, Color.MediumAquamarine },
                { bg, Color.Red },
                { bbg, Color.Orange }
            };
            SaveVolumeToPng(brushVolume, filePath, voxelMapping, Color.Blue);
        }

        // DO NOT USE ONLY FOR DEBUGGING PNGS
        private static Tuple<float, float> MinMaxFloat(float[] volume)
        {
            var max = float.MinValue;
            var min = float.MaxValue;

            for (var i = 0; i < volume.Length; i++)
            {
                var value = volume[i];

                if (Math.Abs(value - short.MinValue) < 1 || Math.Abs(value - short.MaxValue) < 1)
                {
                    continue;
                }

                if (max < value)
                {
                    max = value;
                }

                if (min > value)
                {
                    min = value;
                }
            }

            return Tuple.Create(min, max);
        }

        public static void SaveDistanceVolumeToPng(this Volume2D<float> distanceVolume, string filePath)
        {
            var width = distanceVolume.DimX;
            var height = distanceVolume.DimY;
            var image = new Bitmap(distanceVolume.DimX, distanceVolume.DimY);

            CreateFolderStructureIfNotExists(filePath);

            var minMax = MinMaxFloat(distanceVolume.Array);

            var minimum = minMax.Item1;
            var maximum = minMax.Item2;
            float extval = Math.Min(Math.Min(Math.Abs(minimum), maximum), 3000);

            if (minimum >= 0)
            {
                extval = maximum;
            }
            else if (maximum <= 0)
            {
                extval = Math.Abs(minimum);
            }

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var currentDistanceValue = distanceVolume[x, y];

                    if (currentDistanceValue < -extval) currentDistanceValue = -extval;
                    if (currentDistanceValue > extval) currentDistanceValue = extval;

                    float alpha = (currentDistanceValue - (-extval)) / (2 * extval);

                    float R, G, B;

                    R = 255 * alpha;
                    G = 255 * (1 - alpha);
                    B = 255 * (float)(1 - Math.Abs(alpha - 0.5) * 2);

                    Color color = Color.FromArgb(255, (byte)R, (byte)G, (byte)B);

                    // Background (color intensity for red)
                    if (currentDistanceValue < short.MinValue)
                    {
                        color = Color.Orange;
                    }
                    else if (currentDistanceValue > short.MaxValue)
                    {
                        color = Color.HotPink;
                    }
                    else if ((int)currentDistanceValue == 0)
                    {
                        color = Color.Yellow;
                    }
                    image.SetPixel(x, y, color);
                }
            }

            image.Save(filePath);
        }

        public static byte[] ToByteArray(this Bitmap image)
        {
            var imageWidth = image.Width;
            var imageHeight = image.Height;

            var result = new byte[imageWidth * imageHeight];

            var bitmapData = image.LockBits(new Rectangle(0, 0, imageWidth, imageHeight), ImageLockMode.ReadWrite,
                image.PixelFormat);

            var stride = bitmapData.Stride / imageWidth;

            var pixelData = new byte[Math.Abs(bitmapData.Stride) * imageHeight];

            // Copy the values into the array.
            Marshal.Copy(bitmapData.Scan0, pixelData, 0, pixelData.Length);

            Parallel.For(0, imageHeight, delegate (int y)
            {
                for (var x = 0; x < imageWidth; x++)
                {
                    if (pixelData[y * bitmapData.Stride + (x * stride)] == 0)
                    {
                        result[x + y * imageWidth] = 1;
                    }
                }
            });

            image.UnlockBits(bitmapData);

            return result;
        }

        public static Volume2D<TK> AllocateStorage<T, TK>(this Volume2D<T> volume)
        {
            return new Volume2D<TK>(volume.DimX, volume.DimY, volume.SpacingX, volume.SpacingY, volume.Origin, volume.Direction);
        }

        public static Volume2D<ushort> ToUShortVolume2D(this Volume2D<byte> volume)
        {
            var result = volume.AllocateStorage<byte, ushort>();

            for (int i = 0; i < result.Length; i++)
            {
                result[i] = volume[i];
            }

            return result;
        }

        public static Volume2D<T> Duplicate<T>(this Volume2D<T> volume)
        {
            var result = volume.AllocateStorage<T, T>();

            Array.Copy(volume.Array, result.Array, volume.Array.Length);

            return result;
        }

        // Converts a volume2d into a 3d by creating one slice in Z
        public static Volume3D<T> ToVolume3DInXY<T>(this Volume2D<T> volume, int numberOfZSlices = 1)
        {
            var result = new Volume3D<T>(volume.DimX, volume.DimY, numberOfZSlices, volume.SpacingX, volume.SpacingY, 1);
            Array.Copy(volume.Array, result.Array, volume.Array.Length);
            return result;
        }

        public static Volume2D<T> Duplicate<T>(this ReadOnlyVolume2D<T> volume)
        {
            var result = volume.AllocateStorage<T, T>();

            for (int i = 0; i < volume.Length; i++)
            {
                result[i] = volume[i];
            }

            return result;
        }

        /// <summary>
        /// Returns the Point3D corresponding to the specified index in the specified volume.
        /// Does not check that the index is in range.
        /// </summary>
        public static Point3D GetPoint3D<T>(this Volume3D<T> volume, int index)
        {
            int indexXY, refX, refY, refZ;
            refZ = Math.DivRem(index, volume.DimXY, out indexXY);
            refY = Math.DivRem(indexXY, volume.DimX, out refX);
            return new Point3D(refX, refY, refZ);
        }

        /// <summary>
        /// Sets x, y and z to the coordinates represented by index in volume. Does not check that the index is in range.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void SetXYZ<T>(this Volume3D<T> volume, int index, out int x, out int y, out int z)
        {
            int indexXY;
            z = Math.DivRem(index, volume.DimXY, out indexXY);
            y = Math.DivRem(indexXY, volume.DimX, out x);
        }

        public static void ComputeBinaryMask(this Volume3D<float> computationDistanceVolume, Volume3D<byte> output, float cutLevel = 0, byte foreground = 1, byte background = 0)
        {
            Parallel.For(0, output.DimZ, z =>
            {
                var index = output.DimXY * z;

                for (var i = index; i < output.DimXY + index; i++)
                {
                    output[i] = computationDistanceVolume[i] <= cutLevel ? foreground : background;
                }
            });
        }

        public static void RefineSmoothingWithBrushes(
           Volume<byte> brushes,
           Volume3D<byte> output,
           Region3D<int> region,
           byte foreground,
           byte foregroundBrush)
        {
            Parallel.For(region.MinimumZ, region.MaximumZ + 1, z =>
            {
                for (int y = 0; y < output.DimY; y++)
                {
                    for (int x = 0; x < output.DimX; x++)
                    {
                        var i = output.GetIndex(x, y, z);
                        if (region.MinimumX > x || region.MinimumY > y || region.MaximumX < x || region.MaximumY < y)
                        {
                            continue;
                        }
                        else if (brushes[i] == foregroundBrush)
                        {
                            output[i] = foreground;
                        }
                    }
                }
            });
        }

        public static void ComputeBinaryMask(this Volume3D<float> computationDistanceVolume,
            Volume3D<byte> output,
            Region3D<int> region,
            float cutLevel = 0,
            byte foreground = 1,
            byte background = 0)
        {
            Parallel.For(region.MinimumZ, region.MaximumZ + 1, z =>
            {
                for (int y = 0; y < output.DimY; y++)
                {
                    for (int x = 0; x < output.DimX; x++)
                    {
                        var i = output.GetIndex(x, y, z);
                        byte newValue;
                        if (region.MinimumX > x || region.MinimumY > y || region.MaximumX < x || region.MaximumY < y)
                        {
                            newValue = background;
                        }
                        else
                        {
                            newValue = computationDistanceVolume[i] < cutLevel ? foreground : background;
                        }

                        output[i] = newValue;
                    }
                }
            });
        }

        /// <summary>
        /// Checks if the directory structure for the given file path already exists.
        /// If not, the directory will be created.
        /// </summary>
        /// <param name="filePath"></param>
        public static void CreateFolderStructureIfNotExists(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
        }

        /// <summary>
        /// Computes the integral of the present volume. The result must
        /// use a 64bit format because integrals of large volumes can easily exceed
        /// 32bit integer ranges.
        /// </summary>
        /// <param name="input">The input volume.</param>
        public static Volume3D<long> IntegralImage(this Volume3D<byte> input)
        {
            var output = input.CreateSameSize<long>();
            int dimX = output.DimX;
            int dimY = output.DimY;
            int dimZ = output.DimZ;
            int dimXy = output.DimXY;

            // Point value at origin.
            output[0] = input[0];

            // Values in lines along each axis, with the other two coordinates held at zero.
            for (int x = 1; x < dimX; x++)
            {
                output[x] = input[x] + output[x - 1];
            }

            for (int y = 1; y < dimY; y++)
            {
                output[y * dimX] = input[y * dimX] + output[(y - 1) * dimX];
            }

            for (int z = 1; z < dimZ; z++)
            {
                output[z * dimXy] = input[z * dimXy] + output[(z - 1) * dimXy];
            }

            // Values in bottom plane. The trick here can be visualized as follows:
            //
            // +-----------------------+-+
            // |           B           |D|
            // +-----------------------+-+
            // |                       | |
            // |           A           |C|
            // |                       | |
            // |                       | |
            // O-----------------------+-+
            //
            // Area A extends to (x-1,y-1) from the origin O. B is of size (x-1,1), C is of size (1,x-1),
            // and D is (1,1). U, the union of all four areas extends from the origin to (x,y). So S(U),
            // the sum of intensities in U, is S(A)+S(B)+S(C)+S(D). We have
            //    output[x-1, y-1, 0] = S(A)
            //    output[x-1, y, 0]   = S(A)+S(B)
            //    output[x, y-1, 0]   = S(A)+S(C)
            //    input[x, y, 0]      = S(D)
            // and so S(U) = S(A)+S(B)+S(C)+S(D)
            //             = S(A)+S(B) + S(A)+S(C) + S(D) - S(A)
            //             = output[x-1, y, 0] + output[x, y-1, 0] + input[x, y, 0] - output[x-1, y-1, 0]
            for (int y = 1; y < dimY; y++)
            {
                for (int x = 1; x < dimX; x++)
                {
                    output[x, y, 0] = input[x, y, 0] + output[x - 1, y, 0] + output[x, y - 1, 0] - output[x - 1, y - 1, 0];
                }
            }

            // The same trick as above, but in the plane y=0 rather than z=0.
            for (int z = 1; z < dimZ; z++)
            {
                for (int x = 1; x < dimX; x++)
                {
                    output[x, 0, z] = input[x, 0, z] + output[x - 1, 0, z] + output[x, 0, z - 1] - output[x - 1, 0, z - 1];
                }
            }

            // The same trick as above, but in the plane x=0 rather than z=0.
            for (int z = 1; z < dimZ; z++)
            {
                for (int y = 1; y < dimY; y++)
                {
                    output[0, y, z] = input[0, y, z] + output[0, y - 1, z] + output[0, y, z - 1] - output[0, y - 1, z - 1];
                }
            }

            // Essentially the same trick again, but in three dimensions.
            for (int z = 1; z < dimZ; z++)
            {
                for (int y = 1; y < dimY; y++)
                {
                    for (int x = 1; x < dimX; x++)
                    {
                        output[x, y, z] = input[x, y, z] + output[x, y, z - 1] + output[x, y - 1, z] - output[x, y - 1, z - 1] + output[x - 1, y, z] - output[x - 1, y, z - 1] - output[x - 1, y - 1, z] + output[x - 1, y - 1, z - 1];
                    }
                }
            }

            return output;

        }

        /// <summary>
        /// Creates a volume that contains the differences between a volume and its sagittal mirror volume.
        /// That is, for a voxel on the left side, compute the difference to the mirror voxel on the right side.
        /// This assumes that sagitall mirroring means mirroring across the X dimension of the volume.
        /// </summary>
        /// <param name="input">The input volume.</param>
        /// <returns></returns>
        public static Volume3D<byte> CreateSagittalSymmetricAbsoluteDifference(this Volume3D<byte> input)
        {
            var output = input.CreateSameSize<byte>(0);

            var dimX = output.DimX;