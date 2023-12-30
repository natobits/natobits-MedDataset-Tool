///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace MedILib.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using InnerEye.CreateDataset.Volumes;
    using NUnit.Framework;
    using MedILib;
    using InnerEye.CreateDataset.Math;

    /// <summary>
    ///  A set of tests to test the identification of boundary vocels in structures with different properties
    /// </summary>
    [TestFixture]
    public class MaskBoundariesTest
    {
        // Create cubic structures
        private readonly Volume3D<byte> _inputImageWithBoundaryVoxels =   new Volume3D<byte>(4, 4, 4);
        private readonly Volume3D<byte> _inputImageWithNoBoundaryVoxels = new Volume3D<byte>(4, 4, 4);
        private readonly Volume3D<byte> _inputWithNoForegroundVoxels =    new Volume3D<byte>(4, 4, 4);

        // Expected boundary points
        private readonly Point3D[] _expectedBoundaryVoxels = new Point3D[]
        {
            new Point3D(1,1,1),
            new Point3D(1,1,2),
            new Point3D(1,2,1),
            new Point3D(1,2,2)
        };

        // Set of points that lie on the edges of the structure
        private readonly List<Point3D> _edgeBoundaryVoxels = new List<Point3D>();

        // Setup the images by marking boundaries 
        [SetUp]
        public void Setup()
        {
            var DimX = _inputImageWithBoundaryVoxels.DimX;
            var DimY = _inputImageWithBoundaryVoxels.DimY;
            var DimZ = _inputImageWithBoundaryVoxels.DimZ;

            for (int x = 0; x < DimX; ++x)
            {
                for (int y = 0; y < DimY; ++y)
                {
                    for (int z = 0; z < DimZ; ++z)
                    {
                        var point = new Point3D(x, y, z);
                        _inputImageWithNoBoundaryVoxels[x, y, z] = 1;

                        // Create boundary voxels on the edges of the structure
                        if (_inputImageWithNoBoundaryVoxels.IsEdgeVoxel(x, y, z))
                        {
                            _edgeBoundaryVoxels.Add(point);
                        }

                        // Create a boundary on the bottom left corner of the structure
                        if (x == 0 && z <= 1)
                        {
                            _inputImageWithBoundaryVoxels[x, y, z] = 0;
                        }
                        else
  