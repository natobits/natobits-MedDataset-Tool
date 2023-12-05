///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace InnerEye.CreateDataset.Math.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using InnerEye.CreateDataset.Volumes;
    using InnerEye.CreateDataset.Contours;
    using System.Drawing;
    using NUnit.Framework;

    [TestFixture]
    public class ContourExtensionsTests
    {
        [Description("Tests getting the region from a collection of contours on a slice returns the correct result.")]
        [Test]
        public void GetRegionContoursTest()
        {
            var contours = new List<ContourPolygon>()
            {
                new ContourPolygon(new PointF[]
                {
                    new PointF(5, 10),
                    new PointF(12, 45),
                    new PointF(87, 2),
                    new PointF(234, 5)
                },
                0),
                new ContourPolygon(new PointF[]
                {
                    new PointF(5, 10),
                    new PointF(12, 45),
                    new PointF(1, 23),
                    new PointF(12, 44),
                    new PointF(15, 48),
                },
                0),
                new ContourPolygon(new PointF[]
                {
                    new PointF(5, 10),
                    new PointF(12, 45),
                },
                0)
            };

            var region = contours.GetRegion();

            Assert.AreEqual(1, region.MinimumX);
            Assert.AreEqual(2, region.MinimumY);
            Assert.AreEqual(234, region.MaximumX);
            Assert.AreEqual(48, region.MaximumY);

            Assert.T