
///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace MedLib.IO.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Dicom;
    using InnerEye.CreateDataset.Volumes;    
    using MedLib.IO.RT;
    using MedLib.IO.Readers;

    public static class DicomExtensions
    {
        /// <summary>
        /// DICOM Code String (CS) String literal for the types of contours we produce
        /// </summary>
        public const string ClosedPlanarString = "CLOSED_PLANAR";

        public static string GetStringOrEmpty(this DicomDataset ds, DicomTag tag)
        {
            return ds.GetSingleValueOrDefault(tag, string.Empty);
        }

        /// <summary>
        /// In general DICOM values have an even byte length when serialized. fo-dicom correctly adds a padding value to those VRs 
        /// requiring a pad byte. However the actual padding value used by other implementations can vary, often 0x00 is used instead
        /// of 0x20 (specificed as the padding byte in DICOM), fo-dicom will not remove these on deserialization. We add this method
        /// so users can selectively remove erroneous trailing white space characters and preserve the validity of our output dicom. 
        /// Use with caution, it is only valid for all VRs encoded as character strings BUT NOT VR UI (Unique Identifier) types. 
        /// </summary>
        /// <see cref="ftp://dicom.nema.org/medical/DICOM/2013/output/chtml/part05/sect_6.2.html"/>
        /// <param name="ds"></param>
        /// <param name="tag"></param>
        /// <returns></returns>
        public static string GetTrimmedStringOrEmpty(this DicomDataset ds, DicomTag tag)