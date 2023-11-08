///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace InnerEye.CreateDataset.Math
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using InnerEye.CreateDataset.Contours;

    public static class LinearInterpolationHelpers
    {
        /// <summary>
        /// Linear interpolates between the locked contours. This algorithm expects the contours to be created using our contour
        /// extraction code (i.e. ordered and top left contour extracted first). This will not work on contours not extracted
        /// from a binary mask.
        /// </summary>
        /// <param name="lockedContours">The locked contours.</param>
        /// <returns>The locked contours and the interpolated contours.</returns>
        public static ContoursPerSlice LinearInterpolate<T>(Volumes.Volume3D<T> parentVolume, ContoursPerSlice lockedContours)
        {
            if (lockedContours == null)
            {
                throw new ArgumentNullException(nameof(lockedContours));
            }

            if (parentVolume == null)
            {
                throw new ArgumentNullException(nameof(parentVolume));
            }

            var lockedSlicesIndex = lockedContours.Select(x => x.Key).OrderBy(x => x).ToList();

            // If we have one or 0 locked slices, we don't need to interpolate, so we can return the input
            if (lockedSlicesIndex.Count <= 1)
            {
                return lockedContours;
            }

            Volumes.Volume2D<byte> tempExtractContoursVolume = null;
            
            var currentLockedSlice = lockedSlicesIndex[0];
            var currentLockedContours = lockedContours.ContoursForSlice(currentLockedSlice);

            // Make sure we add the current locked contours into the result
            var result = new Dictionary<int, IReadOnlyList<ContourPolygon>> { [currentLockedSlice] = currentLockedContours };

            // Loop over all locked slices
            for (var i = 1; i < lockedSlicesIndex.Count; i++)
            {
                var nextLockedSlice = lockedSlicesIndex[i];
                var nextLockedContours = lockedContours.ContoursForSlice(nextLockedSlice);

                // Now we have the current and next slice, we need to calculate all the interpolated slices between these two
                for (var newSlice = currentLockedSlice + 1; newSlice < nextLockedSlice; newSlice++)
                {
                    result[newSlice] = LinearInterpolate(currentLockedContours, currentLockedSlice, nextLockedContours, nextLockedSlice, newSlice);

                    // Only allocate memory if needed
                    if (tempExtractContoursVolume == null)
                    {
                        tempExtractContoursVolume = parentVolume.AllocateSlice<T,byte>(Volumes.SliceType.Axial);
                    }
                    else
                    {
                        Array.Clear(tempExtractContoursVolume.Array, 0, tempExtractContoursVolume.Length);
                    }

                    // If we have created any contours we need to rasterize and extract to make sure we don't have intersecting contours on the same slice.
                    tempExtractContoursVolume.Fill<byte>(result[newSlice], 1);
                    result[newSlice] = tempExtractContoursVolume.ContoursWithHoles(1);
                }

                // Make sure we add the locked contours into the result
                result[nextLockedSlice] = nextLockedContours;

                currentLockedSlice = nextLockedSlice;
                currentLockedContours = nextLockedContours;
            }

            return new ContoursPerSlice(result);
        }

        /// <summary>
        /// Interpolations linear between two collection of contours on different slices.
        /// </summary>
        /// <param name="contour1"></param>
        /// <param name="contour1Slice"></param>
        /// <param name="contour2"></param>
        /// <param name="contour2Slice"></param>
        /// <param name="interpolationSlice"></param>
        /// <returns></returns>
        private static IReadOnlyList<ContourPolygon> LinearInterpolate(
            IReadOnlyList<ContourPolygon> contour1, 
            int contour1Slice,
            IReadOnlyList<ContourPolygon> contour2, 
            int contour2Slice, 
            int interpolationSlice)
        {
            v