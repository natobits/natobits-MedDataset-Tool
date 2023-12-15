///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

namespace InnerEye.CreateDataset.Volumes
{
    using System;

    [Obsolete("All contour-related code should move to using the new classes in the InnerEye.CreateDataset.Contours namespace.")]
    public struct ContourStats
    {
        public ContourStats(double sizeIncc, double mean, double standardDeviation)
        {
            SizeIncc = sizeIncc;
            Mean = mean;
            StandardDeviation = standardDeviation;
        }

        // cm^3 or cc
        public double SizeIncc { get; }

        public double Mean { get; }

        public double StandardDeviation { get; }
    }

    [Obsolete("All contour-related code should move to using the new classes in the InnerEye.CreateDataset.Contours namespace.")]
    public static class ContourStatsExtensions
    {
        public static ContourStats CalculateContourStats(ReadOnlyVolume3D<short> originalVolume, Volume3D<byte> contourVolume, byte foreground = 1)
        {
    