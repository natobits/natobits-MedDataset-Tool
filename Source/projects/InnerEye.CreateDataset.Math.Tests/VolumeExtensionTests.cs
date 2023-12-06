///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace InnerEye.CreateDataset.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using InnerEye.CreateDataset.Common;
    using InnerEye.CreateDataset.TestHelpers;
    using InnerEye.CreateDataset.Math;
    using InnerEye.CreateDataset.Volumes;
    using NUnit.Framework;

    [TestFixture]
    public class VolumeExtensionTests
    {
        [Test]
        public void GetFullRegion()
        {
            var volume = new Volume3D<byte>(2, 3, 4);
            var region = volume.GetFullRegion();
            Assert.AreEqual(0, region.MinimumX);
            Assert.AreEqual(0, region.MinimumY);
            Assert.AreEqual(0, region.MinimumZ);
            Assert.AreEqual(1, region.MaximumX);
            Assert.AreEqual(2, region.MaximumY);
            Assert.AreEqual(3, region.MaximumZ);
        }

        [TestCase(-1.0f, 0)]
        [TestCase(0f, 0)]
        [TestCase(1.0f, 255)]
        // Conversion to byte uses Math.Round. Anything that is closer than 1/512 to 1.0
        // will be rounded up to 255, otherwise down to 254
        [TestCase(1.0f - 1.1f / 512f, 254)]
        [TestCase(1.0f - 0.9f / 512f, 255)]
        [TestCase(1.1f, 255)]
        [TestCase(0.499f, 127)]
        [TestCase(0.501f, 128)]
        public void PosteriorToByte(float value, byte expected)
        {
            Assert.AreEqual(expected, Converters.PosteriorToByte(value));
        }

        [TestCase(1.000001f, 1)]
        [TestCase(12345.0001f, 12345)]
        public void TryConvertToInt16Success(float value, short expected)
        {
            var converted = Converters.TryConvertToInt16(value);
            Assert.AreEqual(expected, converted);
        }

        [TestCase(1.00001f)]
        [TestCase(12345.01f)]
        public void TryConvertToInt16Fails(float value)
        {
            var ex = Assert.Throws<ArgumentException>(() => Converters.TryConvertToInt16(value), $"Value {value} should not be converted");
            Assert.IsFalse(ex.Message.Contains("at index -1"), "Index should not be in error message when not provided");
        }

        [TestCase(short.MinValue - 0.1f, short.MinValue)]
        [TestCase(short.MaxValue + 0.1f, short.MaxValue)]
        [TestCase(1.0f, 1)]
        [TestCase(1.5f, 2)]
        [TestCase(14.5f, 15)]
        [TestCase(-0.49f, 0)]
        [TestCase(-0.5f, -1)]
        [TestCase(-100f, -100)]
        public void ConvertAndClampToInt16(float value, short expected)
        {
            var converted = Converters.ClampToInt16(value);
            Assert.AreEqual(expected, converted);
        }

        [Test]
        public void VolumeMap1()
        {
            var volume = new Volume3D<byte>(2, 3, 4);
            volume[0] = 1;
            volume[23] = 23;
            var mapped = volume.Map(value => value);
            VolumeAssert.AssertVolumesMatch(volume, mapped, "");
        }

        [Test]
        public void VolumeMap2()
        {
            var volume = new Volume3D<byte>(2, 3, 4);
            var expected = new Volume3D<byte>(2, 3, 4);
            foreach (var index in volume.Array.Indices())
            {
                volume[index] = (byte)(index + 1);
                expected[index] = (byte)(index + 2);
            }
            var mapped = volume.MapIndexed(null, (value, index) =>
            {
                Assert.AreEqual(index + 1, value);
                return (byte)(index + 2);
            });
            VolumeAssert.AssertVolumesMatch(expected, mapped, "");
        }

        ///<summary>
        /// Test for median smoothing.
        /// We consider a 4x4 image with fixed byte values and
        /// perform median smoothing with radius equal to 1 (neighborhoods of size 27 voxels)
        /// </summary>
        [TestCase(0)]
        [TestCase(1)]
        [Test]
        public void MedianSmoother4by4Test(int radius)
        {
            // Fill data.
            var data = new byte[]
            {
                201, 233, 149, 120, 119, 144, 243, 41, 128, 201, 144, 70, 164,
                8, 14, 56, 133, 160, 73, 216, 24, 83, 63, 191, 195, 26, 38, 45,
                196, 19, 183, 159, 133, 205, 10, 153, 34, 153, 193, 73, 119, 82,
                90, 108, 99, 159, 192, 92, 171, 103, 12, 124, 63, 99, 158, 41,
                150, 87, 4, 51, 9, 45, 221, 83
            };

            // Target values.
  