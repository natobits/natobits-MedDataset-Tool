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
            var result = new List<StatisticValue>();
            var dimInfoList = ExtractDimensionData(binaries);
            if (dimInfoList == null)
            {
                return result;
            }
            var intensityLists = binariesAndRegions
                .Select(pair => GetIntensityList(pair.Item1, pair.Item2, image))
                .ToList();
            var coordinateHistograms =
                binariesAndRegions.Select(pair => CoordinateHistograms.Create(pair.Item1, pair.Item2)).ToList();
            if (image != null && !isGtAndSegmentation)
            {
                result.AddRange(DeriveSpaceIntensityStatistics(binaries, image));
            }
            // We don't want space offset statistics at all in restricted mode; and when we do want them,
            // we only want them calculated once.
            bool doneSpace = isGtAndSegmentation;
            for (int i = 0; i < structureNames.Count; i++)
            {
                if (extremeValues[i] == null)
                {
                    continue;
                }
                string structure1 = structureNames[i];
                var values1 = extremeValues[i];
                if (i > 0 || !isGtAndSegmentation)
                {
                    result.AddRange(CalculateOffsetStatistics(isGtAndSegmentation, dimInfoList, doneSpace, structure1, values1));
                }
                doneSpace = true; // so space offset statistics are not calculated again.
                if (intensityLists[i] != null)
                {
                    result.AddRange(CalculateIntensityMeanAndSd(image, binariesAndRegions, intensityLists, i, structure1));
                }
                if (image != null)
                {
                    var rocStatistic = CalculateBoundaryRoc(binaries, image, exactBoundaryRoc, regions, i, structure1);
                    if (rocStatistic.Value >= 0)
                    {
                        result.Add(rocStatistic);
                    }
                }
                if (i > 0 || !isGtAndSegmentation)
                {
                    // Compactness
                    result.Add(new StatisticValue("Com", structure1, values1.compactness));
                    // Sphericality
                    result.Add(new StatisticValue("Sph", structure1, values1.sphericality));
                }
                // Step-up and step-down entropies, for spotting rectangles and cuboids
                result.AddRange(GetStepEntropyStatistics(structure1, binariesAndRegions[i].Item1, binariesAndRegions[i].Item2));
                // Relative offsets between structures in X, Y and Z dimensions; also relative intensity ROC values.
                if (structureNames[i] == ExternalStructureName && !pairwiseExternal)
                {
                    continue;
                }
                for (int j = i + 1; j < structureNames.Count; j++)
                {
                    if (extremeValues[j] == null || (structureNames[j] == ExternalStructureName && !pairwiseExternal))
                    {
                        continue;
                    }
                    string structure2 = structureNames[j];
                    var values2 = extremeValues[j];
                    foreach (var dimInfo in dimInfoList)
                    {
                        // Distance between min/mid/max point of two structures in X, Y and Z dimensions.
                        result.AddRange(StructurePairOffsetLines(dimInfo, values1, values2, structure1, structure2));
                    }
                    if (image != null)
                    {
                        var roc = IntensityRoc(intensityLists[i], intensityLists[j]);
                        // Intensity ROC between inside of structure1 and structure2: if >0.5, then structure2 is brighter.
                        if (roc >= 0)
                        {
                            result.Add(new StatisticValue("Irc", structure1, structure2, roc));
                        }
                    }
                    var xyzRocValues = CoordinateRocValues(coordinateHistograms[i], coordinateHistograms[j]);
                    result.AddRange(GetCoordinateRocStatistics(structure1, structure2, xyzRocValues));
                }
            }
            return result;
        }

        private static void WriteTrace(string patient, string msg)
        {
            if (!string.IsNullOrEmpty(patient))
            {
                msg = $"{patient}: {msg}";
            }
            Trace.WriteLine(msg);
        }

        private static StatisticValue CalculateBoundaryRoc(List<Volume3D<byte>> binaries, Volume3D<short> image, bool exactBoundaryRoc, List<Region3D<int>> regions, int i, string structure1)
        {
            double roc = exactBoundaryRoc ?
                GetExactBoundaryRoc(image, binaries[i], regions[i]) :
                GetApproximateBoundaryRoc(image, binaries[i], regions[i]);
            return new StatisticValue("Brc", structure1, roc);

        }

        private static List<StatisticValue> CalculateIntensityMeanAndSd(Volume3D<short> image, List<Tuple<Volume3D<byte>, Region3D<int>>> binariesAndRegions, List<List<short>> intensityLists, int i, string structure1)
        {
            // Mean and SD of intensity within the structure
            var intensityMsd = IntensityMeanAndSd(intensityLists[i]);
            // Can be null if the structure has no voxels or only one voxel.
            var result1 = new List<StatisticValue>();
            if (intensityMsd != null)
            {
                result1 = GetStructureIntensityStatistics(structure1, intensityMsd,
                    GetCentroidStandardErrors(binariesAndRegions[i].Item1, binariesAndRegions[i].Item2, image));
            }

            return result1;
        }

        private static List<StatisticValue> CalculateOffsetStatistics(bool restricted, List<DimensionInformation> dimInfoList,
            bool doneSpace, string structure1, ExtremeInfo values1)
        {
            var result = new List<StatisticValue>();
            foreach (var dimInfo in dimInfoList)
            {
                // Once only for each dimension, add the "space" size itself.
                if (!doneSpace)
                {
                    result.Add(new StatisticValue($"{dimInfo.dimName}sz", "space", dimInfo.sizeInMm));
                }
                result.AddRange(SingleStructureOffsetLines(dimInfo, values1, structure1, restricted));
            }

            return result;
        }

        private static List<StatisticValue> DeriveSpaceIntensityStatistics(List<Volume3D<byte>> binaries, Volume3D<short> image)
        {
            var intensityStats = GetSpaceIntensityStatistics(binaries, image);
            return intensityStats;
        }

        /// <summary>
        /// Returns a list of (up to) six "step entropy ratio" statistics. Statistic names are [XYZ][du]h: XYZ for the
        /// dimension, du for down or up, and h for entropy. Taking Xuh ("X up entropy ratio") as an example: for each
        /// value of x in the range covered by (non-zero values in) "binary", we count the number of times that
        /// binary(x-1,y,z)==0 but binary(x,y,z)=1 (thus, a step "up" -- into the structure -- going from x-1 to x).
        /// We take offsets outside the binary (e.g. x==0 so binary(x-1,y,z) is outside) as having a zero value.
        /// This gives us a histogram of counts over all the values of x. The entropy ratio for this histogram is defined
        /// as the ratio of its entropy to the maximum entropy possible for a histogram with that many x values. 
        /// This ratio must be between 0 and 1; a value near 0 implies the histogram is very peaked, so there
        /// may be a rectangle or cuboid with an edge at that x value, while a value near 1 implies a fairly even
        /// histogram, which is not a cause for concern.
        ///    Similarly, "Xdh" is for steps down (binary(x,y,z)==1 but binary(x+1,y,z)==0), and analogously for Y and Z.
        ///    Normally, we expect Xuh and Xdh to be fairly similar, but they could be different, e.g. in the case
        /// of a structure shaped like a half sphere: if the flat base is pointing downwards, Zdh would be low but Zuh
        /// would be in the usual range.
        /// </summary>
        /// <param name="structure"></param>
        /// <param name="binary"></param>
        /// <param name="region"></param>
        /// <returns></returns>
        private static List<StatisticValue> GetStepEntropyStatistics(string structure, Volume3D<byte> binary, Region3D<int> region)
        {
            var result = new List<StatisticValue>();
            if (region.IsEmpty())
            {
                return result;
            }
            var xDownHist = new int[region.MaximumX - region.MinimumX + 1];
            var xUpHist = new int[region.MaximumX - region.MinimumX + 1];
            var yDownHist = new int[region.MaximumY - region.MinimumY + 1];
            var yUpHist = new int[region.MaximumY - region.MinimumY + 1];
            var zDownHist = new int[region.MaximumZ - region.MinimumZ + 1];
            var zUpHist = new int[region.MaximumZ - region.MinimumZ + 1];
            for (var xRel = 0; xRel <= region.MaximumX - region.MinimumX; xRel++)
            {
                var xAbs = xRel + region.MinimumX;
                for (var yRel = 0; yRel <= region.MaximumY - region.MinimumY; yRel++)
                {
                    var yAbs = yRel + region.MinimumY;
                    for (var zRel = 0; zRel <= region.MaximumZ - region.MinimumZ; zRel++)
                    {
                        var zAbs = zRel + region.MinimumZ;
                        var index = binary.GetIndex(xAbs, yAbs, zAbs);
                        if (binary[index] > 0)
                        {
                            if (xAbs == region.MaximumX || binary[index + 1] == 0)
                            {
                                xDownHist[xRel]++;
                            }
                            if (xAbs == region.MinimumX || binary[index - 1] == 0)
                            {
                                xUpHist[xRel]++;
                            }
                            if (yAbs == region.MaximumY || binary[index + binary.DimX] == 0)
                            {
                                yDownHist[yRel]++;
                            }
                            if (yAbs == region.MinimumY || binary[index - binary.DimX] == 0)
                            {
                                yUpHist[yRel]++;
                            }
                            if (zAbs == region.MaximumZ || binary[index + binary.DimXY] == 0)
                            {
                                zDownHist[zRel]++;
                            }
                            if (zAbs == region.MinimumZ || binary[index - binary.DimXY] == 0)
                            {
                                zUpHist[zRel]++;
                            }
                        }
                    }
                }
            }
            AddStepEntropyStatistic("Xdh", structure, xDownHist, result);
            AddStepEntropyStatistic("Xuh", structure, xUpHist, result);
            AddStepEntropyStatistic("Ydh", structure, yDownHist, result);
            AddStepEntropyStatistic("Yuh", structure, yUpHist, result);
            AddStepEntropyStatistic("Zdh", structure, zDownHist, result);
            AddStepEntropyStatistic("Zuh", structure, zUpHist, result);
            return result;
        }

        private static void AddStepEntropyStatistic(string stat, string structure, int[] hist, List<StatisticValue> result)
        {
            var ratio = EntropyRatio(hist);
            if (ratio >= 0) // A negative value is produced when entropy ratio does not exist, e.g. only one slice
            {
                result.Add(new StatisticValue(stat, structure, ratio));
            }
        }
        /// <summary>
        /// Given a histogram of counts, returns the ratio between the entropy of the values in the histogram, and
        /// the entropy it would have if it were totally uniform. This will be zero if all the counts are in one
        /// bucket, and one if the histogram is uniform; otherwise, in between. If the histogram has only one (or no)
        /// values, return -1, meaning "ignore this".
        /// </summary>
        /// <param name="hist"></param>
        /// <returns></returns>
        private static double EntropyRatio(int[] hist)
        {
            if (hist.Length < 2)
            {
                return -1;
            }
            double sumXLogX = 0;
            int sumX = 0;
            foreach (var count in hist)
            {
                if (count > 1)
                {
                    sumXLogX += count * Math.Log(count);
                    sumX += count;
                }
            }
            if (sumX == 0)
            {
                return -1;
            }
            var entropy = Math.Log(sumX) - sumXLogX / sumX;
            var maxEntropy = Math.Log(hist.Length);
            return entropy / maxEntropy;
        }

        // Holder for information specific to a given dimension of the image:
        //   dimName: "X", "Y" or "Z"
        //   sizeInMm: size of the whole image space in mm, in dimension dimName.
        //   numberOfSlices: number of slices in the given dimension.
        private struct DimensionInformation
        {
            public DimensionInformation(string dimName, double spacingInMm, int numberOfSlices)
            {
                this.dimName = dimName;
                this.spacingInMm = spacingInMm;
                this.numberOfSlices = numberOfSlices;
                sizeInMm = spacingInMm * (numberOfSlices - 1);
            }

            public string dimName;
            public double spacingInMm;
            public int numberOfSlices;
            public double sizeInMm;
        }

        private static List<DimensionInformation> ExtractDimensionData(List<Volume3D<byte>> binaries)
        {
            var example = binaries.FirstOrDefault(x => x != null);
            if (example == null)
            {
                return null;
            }
            return new List<DimensionInformation>
            {
                new DimensionInformation("X", example.SpacingX, example.DimX),
                new DimensionInformation("Y", example.SpacingY, example.DimY),
                new DimensionInformation("Z", example.SpacingZ, example.DimZ)
            };
        }

        private static List<StatisticValue> GetCoordinateRocStatistics(string structure1,
            string structure2, RocTriple rocs)
        {
            if (rocs != null)
            {
                // Coordinate ROC values between structure1 and structure 2 in X, Y and Z dimensions: if
                // >0.5, then structure2 has in general higher coordinate values in that dimension.
                return new List<StatisticValue>()
                {
                    new StatisticValue("Xrc", structure1, structure2, rocs.XRoc),
                    new StatisticValue("Yrc", structure1, structure2, rocs.YRoc),
                    new StatisticValue("Zrc", structure1, structure2, rocs.ZRoc)
                };
            }
            return new List<StatisticValue>();
        }

        private static List<StatisticValue> GetSpaceIntensityStatistics(List<Volume3D<byte>> binaries, Volume3D<short> image)
        {
            // Intensity of whole space
            var spaceIntensityMsd = IntensityMeanAndSd(image.Array);
            // Intensity of background voxels
            var backgroundIntensityMsd = BackgroundIntensityMeanAndSd(image, binaries);
            return new List<StatisticValue>
         