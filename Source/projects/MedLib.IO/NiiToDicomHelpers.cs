///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace MedLib.IO
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;

    using Dicom;
    using Dicom.IO.Buffer;

    using MedLib.IO.Models;
    using MedLib.IO.Models.DicomRt;
    using MedLib.IO.Readers;
    using MedLib.IO.RT;
    using MedLib.IO.Writers;

    using InnerEye.CreateDataset.Contours;
    using InnerEye.CreateDataset.Volumes;

    using MoreLinq;

    public enum ImageModality
    {
        /// <summary>
        /// Computer Tomography image modality.
        /// </summary>
        CT,

        /// <summary>
        /// Magnetic Resonance image modality.
        /// </summary>
        MR,
    }

    /// <summary>
    /// Helper class for converting Nii to Dicom. 
    /// Use with extreme care - many Dicom elements have to be halluzinated here, and there's no 
    /// guarantee that the resulting Dicom will be usable beyond what is needed in InnerEye.
    /// </summary>
    public static class NiiToDicomHelpers
    {
        /// <summary>
        /// The maximum number of characters in a string that is valid in a Dicom Long String element, see
        /// http://dicom.nema.org/dicom/2013/output/chtml/part05/sect_6.2.html
        /// </summary>
        public const int MaxDicomLongStringLength = 64;

        /// <summary>
        /// Converts from channeldId to ImageModality. Any channel name 'CT' (case insensitve) is
        /// recognized as CT, all other channel names as MR.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <returns></returns>
        public static ImageModality InferModalityFromChannelId(string channelId)
        {
            if (channelId.Equals("ct", StringComparison.InvariantCultureIgnoreCase))
            {
                return ImageModality.CT;
            }

            // If not CT, assume MR
            return ImageModality.MR;
        }

        /// <summary>
        /// Creates a set of Dicom items that contain information about the manufacturer of the scanner,
        /// and other information.
        /// </summary>
        /// <param name="manufacturer">The value to use for the DicomTag.Manufacturer element.</param>
        /// <param name="studyId">The value to use for the DicomTag.StudyID tag</param>
        /// <param name="patientName">The value to use for the DicomTag.PatientName tag.</param>
        /// <returns></returns>
        public static DicomDataset DatasetWithExtraInfo(string manufacturer = null,
            string studyId = null,
            string patientName = null)
        {
            if (!IsVali