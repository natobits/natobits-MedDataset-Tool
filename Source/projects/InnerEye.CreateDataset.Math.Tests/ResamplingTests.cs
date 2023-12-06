///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace InnerEye.CreateDataset.Math.Tests
{
    using System.Linq;
    using InnerEye.CreateDataset.Contours;
    using InnerEye.CreateDataset.Math;
    using InnerEye.CreateDataset.Volumes;

    using NUnit.Framework;

    [TestFixture]
    public class ResamplingTests
    {
        [TestCase(0.5)]
        [TestCase(0.8)]
        [TestCase(0.6)]
        [TestCase(1.0)]
        [TestCase(2.0)]

        public void CheckResamplingNearestAndLinear(double factorXY)
        {
            int dimX = 100;
            int dimY = 100;
            int dimZ = 3;
            int spacingX = 1;
            int spac