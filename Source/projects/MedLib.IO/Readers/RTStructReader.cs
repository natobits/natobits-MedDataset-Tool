///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace MedLib.IO
{
    using System;
    using System.Collections.Generic;
    using System.Drawing;
    using System.Linq;
    using System.Threading.Tasks;
    using Dicom;
    using MedLib.IO.Extensions;
    using MedLib.IO.Models.DicomRt;
    using MedLib.IO.RT;
    using InnerEye.CreateDataset.Contours;
    using InnerEye.CreateDataset.Volumes;

    public class RtStructReader
    {              
        public static Tuple<RadiotherapyStruct, string> LoadContours(
            string filePath, Transform3 dicomToData, string seriesUID = null, string studyUID = null, bool warningsAsErrors = true)
        {
            var file = DicomFile.Open(filePath);
            return LoadContours(file.Dataset, dicomToData, seriesUID, studyUID, warningsAsErrors);
        }

        /// <summary>
        /// Load a RadiotherapyStruct from the given dicom dataset and map into the coordinate of the given Volume3D
        /// </summary>
        /// <param name="ds">Dataset to read the structure set from</param>
        /// <param name="dicomToData">The transform from going between dicom and voxel points.</param>
        /// <param name="seriesUID">SeriesUID that must match the referenced seriesUID inside the structure set</param>
        /// <param name="studyUID">The structure set must belong to the same study</param>
        /// <param name="warningsAsErrors">true if warnings should be treated as errors and thrown from this method.</param>
        /// <returns>A new RadiotherapyStruct with any warnings collated into a string</returns>
        public static Tuple<RadiotherapyStruct, string> LoadContours(
           DicomDataset ds, Transform3 dicomToData, string seriesUID = null, string studyUID = null, bool warningsAsErrors = true)
        {
            RadiotherapyStruct rtStruct = RadiotherapyStruct.Read(d