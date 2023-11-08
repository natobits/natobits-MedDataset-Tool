///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

namespace InnerEye.CreateDataset.Math
{
    using System;
    using Volumes;
    using ImageProcessing;
    using Morphology;
    using System.Linq;

    public static class MorphologicalExtensions
    {
        /// <summary>
        /// Erode the input mask by the same margin in each dimension
        /// </summary>
        /// <param name="input">The volume is a binary mask where 1 represents foreground and 0 represents background</param>
        /// <param name="mmMargin">Erosion in the x,y and z dimension</param>
        /// <param name="structuringElement">Structuring element to use (the default implementation is an ellipsoid)</param>
        /// <returns>the eroded structure: value is 1 inside the structure, 0 outside.</returns>
        public static Volume3D<byte> Erode(this Volume3D<byte> volume, double mmMargin, StructuringElement structuringElement = null)
        {
            return DilateErode(volume, mmMargin, mmMargin, mmMargin, true, null, structuringElement);
        }

        /// <summary>
        /// Dilate the input mask by the same margin in each dimension, taking into account the restriction volume 
        /// </summary>
 