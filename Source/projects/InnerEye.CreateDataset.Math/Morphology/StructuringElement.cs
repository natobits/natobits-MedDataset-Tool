///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿using InnerEye.CreateDataset.Volumes;
using System.Collections.Generic;

namespace InnerEye.CreateDataset.Math.Morphology
{
    /// <summary>
    ///  An ellipsoid structuring element (SE) for use in morphological operations
    ///  
    ///  1) We create a mask (a cuboid with radius equal to the dilation/erosion radius in each dimension) to hold the ellipsoid
    ///  and use the equation of the ellipsoid to mark points that lie inside it as foreground
    ///  2) We extract all of the surface points (points that lie on the edge or have a BG neighbor in their 1-con