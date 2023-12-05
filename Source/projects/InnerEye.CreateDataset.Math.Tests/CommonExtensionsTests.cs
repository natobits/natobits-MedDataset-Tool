///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace InnerEye.CreateDataset.Math.Tests
{
    using System;
    using InnerEye.CreateDataset.TestHelpers;
    using InnerEye.CreateDataset.Volumes;
    using InnerEye.CreateDataset.Math;
    using NUnit.Framework;

    [TestFixture]
    public class CommonExtensionsTests
    {
        [Test]
        public void CommonExtensionMinMaxInvalid()
        {
            Volume3D<byte> volume = null;
            short[] array = null;
            Assert.Throws<ArgumentNullException>(() => volume.GetMinMax());
            Assert.Throws<ArgumentNullException>(() => array.GetMinMax());
            Assert.Throws<ArgumentNullException>(() => array.Minimum());
            Assert.Throws<ArgumentNullException>(() => array.Maximum());
            var empty = new short[0];
            Assert.Throws<ArgumentException>(() => empty.GetMinMax());
            Assert.Throws<ArgumentException>(() => empty.Minimum());
            Assert.Throws<ArgumentException>(() => empty.Maximum());
        }

        [Test]
        [TestCase(new byte[] { 1 }, (byte)1, (byte)1)]
        [TestCase(new byte[] { 10, 2, 5, 5}, (byte)2, (byte)10)]
        [TestCase(new byte[] { 10, 2, 20, 1}, (byte)1, (byte)20)]
        public void CommonExtensionMinMax(byte[] values, byte expectedMin, byte expectedMax)
        {
            var min = values.Minimum();
            var max = values.Maximum();
            var minMax = values.GetMinMax();
            Assert.AreEqual(expectedMin, min, "Minimum");
            Assert.AreEqual(expectedMax, max, "Maximum");
            Assert.AreEqual(expectedMin, minMax.Minimum, "MinMax.Minimum");
            Assert.AreEqual(expectedMax, minMax.Maximum, "MinMax.Maximum");
        }

        [Test]
        public void CommonExtensionsEmptyRegion()
        {
            var region = RegionExtensions.EmptyIntRegion();
            Assert.IsTrue(region.IsEmpty());
            Assert.AreEqual(0, region.MinimumX);
            Assert.AreEqual(-1, region.MaximumX);
            Assert.AreEqual(0, region.Size());
            Assert.AreEqual(0, region.LengthX());
            Assert.AreEqual(0, region.LengthY());
            Assert.AreEqual(0, region.LengthY());
            Assert.IsFalse(region.ContainsPoint(0, 0, 0));
        }

        [Test]
        public void CommonExtensionsRegionLength()
        {
            var minX = 10;
            var maxX = 11;
            var minY = 20;
            var maxY = 22;
            var minZ = 30;
            var maxZ = 33;
            var region = new Region3D<int>(minX, minY, minZ, maxX, maxY, maxZ);
            Assert.IsFalse(region.IsEmpty());
            Assert.AreEqual(2, region.LengthX());
            Assert.AreEqual(3, region.LengthY());
            Assert.AreEqual(4, region.LengthZ());
            Assert.AreEqual(2 * 3 * 4, region.Size());
            Assert.IsFalse(region.ContainsPoint(minX - 1, minY, minZ));
            Assert.IsFalse(region.ContainsPoint(maxX + 1, minY, minZ));
            Assert.IsTrue(region.ContainsPoint(minX, minY, minZ));
            Assert.IsTrue(region.ContainsPoint(maxX, minY, minZ));
            Assert.IsFalse(region.ContainsPoint(minX, minY - 1, minZ));
            Assert.IsFalse(region.ContainsPoint(minX, maxY + 1, minZ));
            Assert.IsTrue(region.ContainsPoint(minX, minY, minZ));
            Assert.IsTrue(region.ContainsPoint(minX, maxY, minZ));
            Assert.IsFalse(region.ContainsPoint(minX, minY, minZ - 1));
            Assert.IsFalse(region.ContainsPoint(minX, minY, maxZ + 1));
            Assert.IsTrue(region.ContainsPoint(minX, minY, minZ));
            Assert.IsTrue(region.ContainsPoint(minX, minY, maxZ));
        }

        [Test]
        public void CommonExtensionsRegionInsideOf()
        {
            var minX = 11;
            var maxX = 12;
            var minY = 21;
            var maxY = 23;
            var minZ = 31;
            var maxZ = 34;
            var region = new Region3D<int>(minX, minY, minZ, maxX, maxY, maxZ);
            Assert.IsTrue(region.InsideOf(region), "Regions are equal");
            var outer1 = new Region3D<int>(minX - 1, minY -1, minZ - 1, maxX + 1, maxY + 1, maxZ + 1);
            Assert.IsTrue(region.InsideOf(outer1), "Outer region is larger by a margin of 1");
            var outer2 = new Region3D<int>(0, minY, minZ, 0, maxY, maxZ);
            Assert.IsFalse(region.InsideOf(outer2), "Outer region does not enclose in X dimension");
            var outer3 = new Region3D<int>(minX, 0, minZ, maxX, 0, maxZ);
            Assert.IsFalse(region.InsideOf(outer3), "Outer region does not enclose in Y dimension");
            var outer4 = new Region3D<int>(minX, minY, 0, maxX, maxY, 0);
            Assert.IsFalse(region.InsideOf(outer4), "Outer region does not enclose in Z dimension");
            var empty = RegionExtensions.EmptyIntRegion();
            Assert.Throws<ArgumentException>(() => region.InsideOf(empty));
            Assert.Throws<ArgumentException>(() => empty.InsideOf(region));
        }

        [Test]
        public void CommonExtensionsGetInterestRegion1()
        {
            var volume0 = new Volume3D<byte>(3, 3, 3);
            var region0 = volume0.GetInterestRegion();
            Assert.IsTrue(region0.IsEmpty(), "When there are no non-zero values, the region should be empty");
            // Regions define an equality operator that we can use here
            Assert.AreEqual(RegionExtensions.EmptyIntRegion(), region0, "When no foreground is present, should return the special EmptyRegion");
            Assert.Throws<ArgumentException>(() => volume0.Crop(region0), "Cropping with an empty region should throw an exception");
        }

        [Test]
        public void CommonExtensionsGetInterestRegion2()
        {
            var volume0 = new Volume3D<byte>(2, 3, 4);
            var volume1 = volume0.CreateSameSize<byte>(1);
            var fullRegion = volume1.GetFullRegion();
            var region1 = volume1.GetInterestRegion();
            Assert.AreEqual(fullRegion, region1, "When all voxels have non-zero values, the region should cover the full image.");
            // Find values that are larger or equal than 1: Again, this should be all voxels.
            var region2 = volume1.GetInterestRegion(1);
            Assert.AreEqual(fullRegion, region2, "All voxels have values >= 1, the region should cover the full image.");
           