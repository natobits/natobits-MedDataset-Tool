///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

namespace InnerEye.CreateDataset.Common.Models
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;

    using InnerEye.CreateDataset.Math;
    using InnerEye.CreateDataset.Volumes;

    public class StatisticsCalculator
    {
        /*
         * "CalculateCsvLines" calculates a variety of statistics on the structures represented by the supplied binaries and images.
         * It returns a list of csv rows of the form "patient,statistic,structure1,structure2,value". "patient" is always
         * the supplied patient ID, and "value" is the value of "statistic" for the combination of structure1 and structure2,
         * or just for structure1 if structure2 is empty or equal to structure1. Each "structure" is either one of the names
         * in structureNames, or one of the strings "space" (for the whole space) or "background" (space minus skin).
         *
         * Some statistics are for single structures, some compare two structures. The single-structure ones are:
         *
         * Brc: "boundary ROC". Intensity ROC across the boundary of the structure. A value near 0 or 1 means the boundary
         *      mostly separates regions of different intensity; a value close to 0.5 means it mostly doesn't.
         * Com: "compactness". The ratio of the volume of the structure to the volume of the ellipsoid defined by its minimum
         *      and maximum x, y and z values. Low values (near 0) means the structure is very diffuse and may even have
         *      separated components. Values near 1 represent near-ellipsoids, and values over 1 tend towards cubes.
         * Hma: homogeneity at 3mm. See below for full details. Low values (nearer 0 to 1) mean that location within
         *      the structure strongly predicts intensity, i.e. the structure is not homogeneous.
         * Hmb: homogeneity at 6mm. Like Hma, but predicting each voxel intensity from voxels 6mm away.
         * Hmp: homogeneity at 1 pixel, calculated only within slices.
         * Imu: mean intensity within structure1 (values are taken from "image").
         * Isd: standard deviation of intensity within structure1.
         * Sph: "sphericality". RMS_sphere / RMS, where RMS is the root mean square distance from a voxel in the
         *      structure to the centroid of the structure, and RMS_sphere is the value for a sphere with the same
         *      number of voxels. The closer to 1 a value is, the more spherical the structure is.
         * Vol: total volume of the structure in mm^3.
         * [XYZ]fh: "flatness" at the top of the structure ("high"): number of voxels in top slice of structure divided by mean number
         *      of voxels per slice for the structure, where slices are perpendicular to the [XYZ] axis.
         * [XYZ]fl: as [XYZ]fh, but the bottom slice.
         * [XYZ]mi: number of missing slices in the structure (slices between the top and bottom slices of the structure
         *      that have no voxels in the structure), where slices are perpendicular to the [XYZ] axis.
         * [XYZ]tb: "top and bottom count": total number of voxels in this structure in the top and bottom slices of the image,
         *      where slices are perpendicular to the [XYZ] axis.
         * [XYZ]sz: maximum extent of structure1 in dimension [XYZ], in mm, i.e. max - min value.
         * [XYZ][ud]h: "up entropy ratio" and "down entropy ratio" in the X, Y and Z dimensions. Low values suggest the
         *      presence of flat planes or rectangles in the dimension in question. See GetStepEntropyStatistics
         *      for details.
         * 
         * The two-structure statistics are:
         * 
         * Irc: "intensity ROC" between two structures. Higher values (between 0.5 and 1) mean structure2 is brighter.
         * Ovr: overlap ratio: number of voxels in the intersection of structure1 and structure2, divided by number
         *      of voxels in the smaller of the two structures. High values are a cause for concern.
         * Zde: difference of extremes: maximum value for second structure minus minimum value for first.
         * [XYZ]hi: difference between the maximum {x, y, z} of structure2 and that of structure1.
         * [XYZ]lo: as [XYZ]hi, but minimum.
         * [XYZ]md: as [XYZ]hi, but midpoint.
         * [XYZ]rc: ROC for the {x, y, z} coordinate of structure2 against that of structure1. Values near 0 or 1
         *          means the structures are well separated when projected onto that dimension, values near 0.5 mean
         *          they are not.
         *          
         * If the "image" handed to CalculateCsvLines is null, the intensity-related statistics (Hma, Hmb, Hmp, Imu, Irc, Isd) are
         * not included in the results.
         */

        /// <summary>
        /// Data class for recording the value of a statistic (one of the three-letter codes above) between two structures.
        /// </summary>
        public class StatisticValue
        {
            public readonly string Statistic;
            public readonly string Structure1;
            public readonly string Structure2;
            public readonly double Value;

            public StatisticValue(string statistic, string structure, double value)
            {
                Statistic = statistic;
                Structure2 = Structure1 = structure;
                Value = value;
            }

            public StatisticValue(string statistic, string structure1, string structure2, double value)
            {
                Statistic = statistic;
                Structure1 = structure1;
                Structure2 = structure2;
                Value = value;
            }

            public string CsvRow(int patient)
            {
                return $"{patient},{Statistic},{Structure1},{Structure2},{Value}";
            }
        }

        // Name of the special "external" structure, i.e. the body that contains all the other
        // structures. This could be made a parameter if we changed the convention that it's
        // always called "external".
        private const string ExternalStructureName = "external";

        /// <summary>
        /// Calculates and returns the statistics CSV lines for the given patient. A wrapper
        /// around Calculate, which converts the StatisticsValue objects to CSV lines.
        /// </summary>
        public static List<StatisticValue> CalculateStatisticValues(List<Volume3D<byte>> binaries,
            Volume3D<short> image, List<string> structureNames, bool exactBoundaryRoc, bool pairwiseExternal)
        {
            return Calculate(binaries, image, structureNames, exactBoundaryRoc: exactBoundaryRoc, pairwiseExternal: pairwiseExternal)
                .ToList();
        }

        /// <summary>
        /// Calculate and return a list of StatisticValues for the provided data.
        /// </summary>
        /// <param name="binaries">a mask, one for each structure in structureNames</param>
        /// <param name="image">CT image volume</param>
        /// <param name="structureNames">the names of the structure in binaries</param>
        /// <param name="isGtAndSegmentation">If true, we expect exactly 2 binaries, for ground truth and (predicted) 
        /// segmentation. We only do single-structure calculations the latter (i=1) in that case.</param>
        /// <param name="exactBoundaryRoc">If true, we calculate boundary regions using the standard erode/dilate code,
        /// which takes a long time because it cannot (currently) be restricted to looking at a subset of the space.
        /// Otherwise, we run a much quicker, approximate algorithm.</param>
        /// <returns></returns>
        public static List<StatisticValue> Calculate(List<Volume3D<byte>> binaries, Volume3D<short> image,
            List<string> structureNames, bool isGtAndSegmentation = false, bool exactBoundaryRoc = false,
            bool pairwiseExternal = false)
        {
            // 3D Regions, each one the smallest that encloses the corresponding structure.
            var regions = binaries.Select(b => b?.GetInterestRegion()).ToList();
            var binariesAndRegions = binaries.Zip(regions, Tuple.Create).ToList();
            // extremeValues contains the coordinates of each region, plus the "compactness" and "sphericality" values
            // of the structure.
            List<ExtremeInfo> extremeValues = binariesAndRegions.Select(CalculateExtremes).ToList();
            var result = new Li