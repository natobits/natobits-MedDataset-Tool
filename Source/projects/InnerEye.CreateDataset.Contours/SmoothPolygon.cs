///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace InnerEye.CreateDataset.Contours
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Drawing;
    using System.IO;
    using System.Linq;
    using PointInt = System.Drawing.Point;

    public static class SmoothPolygon
    {
        /// <summary>
        /// When converting a polygon to turtle graphics, use this to indicate "Forward".
        /// </summary>
        public const char TurtleForward = 'F';

        /// <summary>
        /// When converting a polygon to turtle graphics, use this to indicate "Turn Left".
        /// </summary>
        public const char TurtleLeft = 'L';

        /// <summary>
        /// When converting a polygon to turtle graphics, use this to indicate "Turn Right".
        /// </summary>
        public const char TurtleRight = 'R';

        /// <summary>
        /// We current have the following approaches:
        ///     - None: Takes the mask representation of the polygon and return the outer edge
        ///     - Small: Uses a simplistic 'code-book' approach to smoothing the outer edge of the polygon
        /// </summary>
        /// <param name="polygon">The input mask representation of the extracted polygon.</param>
        /// <param name="smoothingType">The smoothing type to use.</param>
        /// <returns>The smoothed polygon.</returns>
        public static PointF[] Smooth(InnerOuterPolygon polygon, ContourSmoothingType smoothingType = ContourSmoothingType.Small)
        {
            var result = SmoothAndMerge(
                polygon,
                (points, isCounterClockwise) => SmoothPoints(points, isCounterClockwise, smoothingType));

            return ContourSimplifier.RemoveRedundantPoints(result);
        }

        /// <summary>
        /// Generates a contour that traces the voxels at the given integer position, and that is smoothed
        /// using the given smoothing level.
        /// </summary>
        /// <param name="points">The set of integer points that describe the polygon.</param>
        /// <param name="isCounterClockwise">If true, the points are an inner contour and are given in CCW order.
        /// Otherwise, assume they are an outer contour and are in clockwise order.</param>
        /// <param name="smoothingType">The type of smoothing that should be applied.</param>
        /// <returns></returns>
        public static PointF[] SmoothPoints(IReadOnlyList<PointInt> points, bool isCounterClockwise, ContourSmoothingType smoothingType)
        {
            switch (smoothingType)
            {
                case ContourSmoothingType.None:
                    return ClockwisePointsToExternalPathWindowsPoints(points, isCounterClockwise, -0.5f);
                case ContourSmoothingType.Small:
                    return SmallSmoothPolygon(points, isCounterClockwise);
                default:
                    throw new NotImplementedException($"There is smoothing method for {smoothingType}");
            }
        }

        /// <summary>
        /// Finds the index of the PointF such that the line from points[index] to points[index+1]
        /// intersects the vertical line at start.X, and where the Y coordinate is smaller than
        /// start.Y. If there are multiple intersections, return
        /// the one with maximum Y coordinate if <paramref name="searchForHighestY"/> is true.
        /// This is used to search for the lowest PointF (highest Y) in a parent contour that is
        /// above the child contour.
        /// If <paramref name="searchForHighestY"/> is false, find the intersection with minimum Y coordinate,
        /// without constraints on start.Y.
        /// </summary>
        /// <param name="points"></param>
        /// <param name="x"></param>
        /// <returns></returns>
        public static int FindIntersectingPoints(IReadOnlyList<PointF> points, PointInt start, bool searchForHighestY)
        {
            float? bestY = null;
            var bestIndex = -1;
            var startX = start.X;

            // When merging a child contour with a parent contour: Points is the parent contour, start
            // is the search start PointF of the inner (child) contour.
            // Search for the line segment of the parent that is closest above the child start PointF.
            // The child is assumed to be fully contained in the parent, hence such a PointF must exist.
            // Among those points, search for the PointF that is closest to the child contour, above the child contour.
            var PointF = points[0];
            for (var index = 1; index <= points.Count; index++)
            {
                var nextPoint = index == points.Count ? points[0] : points[index];

                // A line segment above the start PointF can come from either side, depending on the winding of the parent
                // contour
                if ((PointF.X <= startX && nextPoint.X > startX)
                    || (nextPoint.X < startX && PointF.X >= startX))
                {
                    var isAboveStart = PointF.Y <= start.Y || nextPoint.Y <= start.Y;
                    if ((searchForHighestY && isAboveStart && (bestY == null || PointF.Y > bestY))
                        || (!searchForHighestY && (bestY == null || PointF.Y < bestY)))
                    {
                        bestIndex = index - 1;
                        bestY = PointF.Y;
                    }
                }

                PointF = nextPoint;
            }

            if (bestIndex < 0)
            {
                throw new ArgumentException($"Inconsistent arguments: The polygon does not contain a line segment that intersects at X = {startX}.", nameof(points));
            }

            return bestIndex;
        }

        /// <summary>
        /// Gets the PointF at which the line between point1 