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
        /// Gets the PointF at which the line between point1 and point2 attains the given x value.
        /// </summary>
        /// <param name="point1"></param>
        /// <param name="point2"></param>
        /// <param name="x"></param>
        /// <returns></returns>
        public static PointF IntersectLineAtX(PointF point1, PointF point2, int x)
        {
            var direction = point2.Subtract(point1);
            if (Math.Abs(direction.X) < 1e-10)
            {
                return point1;
            }

            var deltaX = x - point1.X;
            return new PointF(x, point1.Y + deltaX * direction.Y / direction.X);
        }

        /// <summary>
        /// Connects a parent contour (outer rim of a structure) with a child contour that represents the inner rim
        /// of a structure with holes. The connection is done via a vertical line from the starting PointF of the child contour
        /// to a PointF above (smaller Y coordinate) in the parent contour.
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="childStartingPoint"></param>
        /// <returns></returns>
        public static PointF[] ConnectViaVerticalLine(PointF[] parent, PointF[] child, PointInt childStartingPoint)
        {
            if (parent == null || parent.Length == 0)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (child == null || child.Length == 0)
            {
                throw new ArgumentNullException(nameof(child));
            }

            var parentIndex1 = FindIntersectingPoints(parent, childStartingPoint, searchForHighestY: true);
            var childIndex1 = FindIntersectingPoints(child, childStartingPoint, searchForHighestY: false);
            (int, PointF) GetIntersection(PointF[] points, int index)
            {
                var index2 = index == points.Length - 1 ? 0 : index + 1;
                return (index2, IntersectLineAtX(points[index], points[index2], childStartingPoint.X));
            }

            var (_, connectionPointParent) = GetIntersection(parent, parentIndex1);
            var (_, connectionPointChild) = GetIntersection(child, childIndex1);
            var connectionPoints = new PointF[] { connectionPointParent, connectionPointChild };
            return InsertChildIntoParent(parent, parentIndex1, child, childIndex1 + 1, connectionPoints);
        }

        /// <summary>
        /// Splices an array ("parent array") with contour points, and inserts a child contour into.
        /// The result will be composed of the following parts:
        /// * The first entries of the parent array, until and including the <paramref name="insertPositionInParent"/> index
        /// * The connection points in <paramref name="connectingPointsFromParentToChild"/>
        /// * The entries of the child array starting at <paramref name="childStartPosition"/>
        /// * The entries of the child array from 0 to before the <paramref name="childStartPosition"/>
        /// * The connection points in reverse order
        /// * then the remaining entries from the parent array, starting at <paramref name="insertPositionInParent"/> + 1.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="parent"></param>
        /// <param name="insertPositionInParent">The position in the parent array at which the child should be inserted.
        /// Must be in the range 0 .. parent.Length.</param>
        /// <param name="child"></param>
        /// <param name="childStartPosition">The element of the child array that should be inserted first.</param>
        /// <param name="connectingPointsFromParentToChild">The points that connect parent and child.</param>
        /// <returns></returns>
        public static T[] InsertChildIntoParent<T>(
            T[] parent,
            int insertPositionInParent,
            T[] child,
            int childStartPosition,
            T[] connectingPointsFromParentToChild)
        {
            parent = parent ?? throw new ArgumentNullException(nameof(parent));
            connectingPointsFromParentToChild = connectingPointsFromParentToChild ?? throw new ArgumentNullException(nameof(connectingPointsFromParentToChild));

            if (child == null || child.Length == 0)
            {
                throw new ArgumentNullException(nameof(child));
            }

            if (insertPositionInParent < 0 || insertPositionInParent > parent.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(insertPositionInParent));
            }

            if (childStartPosition < 0 || childStartPosition >= child.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(insertPositionInParent));
            }

            var connectionLength = connectingPointsFromParentToChild.Length;
            var result = new T[parent.Length + connectionLength + child.Length + connectionLength];
            var insertAt = 0;
            void Insert(T[] sourceArray, int sourceIndex, int length)
            {
                Array.Copy(sourceArray, sourceIndex, result, insertAt, length);
                insertAt += length;
            }

            // Copy the first elements up to and including the insertPosition directly from parent.
            Insert(parent, 0, insertPositionInParent + 1);

            // The points that make up the connection from the parent to the child starting PointF.
            Insert(connectingPointsFromParentToChild, 0, connectionLength);

            // Then follow the elements from child, up to the end of the child array.
            Insert(child, childStartPosition, child.Length - childStartPosition);

            // The remaining elements from the beginning of the child array
            Insert(child, 0, childStartPosition);

            // Add the connecting points in reverse order, back to the parent.
            for (var index = connectionLength; index > 0; index--)
            {
                result[insertAt++] = connectingPointsFromParentToChild[index - 1];
            }

            // Finally the remaining elements from the parent.
            if (insertPositionInParent < parent.Length - 1)
            {
                Insert(parent, insertPositionInParent + 1, parent.Length - insertPositionInParent - 1);
            }

            return result;
        }

        /// <summary>
        /// Smoothes a PointF polygon that contains an inner and an outer rim. Inner and outer rim are
        /// smoothed separately, and then connected via a zero width "channel": The smoothed
        /// contour will first follow the outer polygon, then go to the inner contour, follow
        /// the inner contour, and then go back to the outer contour.
        /// </summary>
        /// <param name="polygon"></param>
        /// <param name="smoothingFunction"></param>
        /// <returns></returns>
        private static PointF[] SmoothAndMerge(InnerOuterPolygon polygon, Func<IReadOnlyList<PointInt>, bool, PointF[]> smoothingFunction)
        {
            var result = smoothingFunction(polygon.Outer.Points, false);
            foreach (var inner in polygon.Inner)
            {
                result = ConnectViaVerticalLine(result, smoothingFunction(inner.Points, true), inner.StartPointMinimumY);
            }

            return result;
        }

        private static PointF[] SmallSmoothPolygon(IReadOnlyList<PointInt> polygon, bool isCounterClockwise)
        {
            // The contour simplification code called below expects contours in a string format
            // describing a sequence of unit moves (left, right, straight). The ideal place to
            // compute such a string would be in the code for walking around the contour bounary.
            // But to facilitate early integration, we'll do this here for now.
            var perimeterPath = ClockwisePointsToExternalPathWindowsPoints(polygon, isCounterClockwise, 0.0f);
 