///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace MedLib.IO.Readers
{
    using Dicom;
    using MedLib.IO.RT;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// This class represents DICOM IOD modules associated with a volumetric representation of a DICOM series. 
    /// TODO: DicomSeriesReader repeats much of the activity here - that class should generate this information.
    /// TODO: The DICOM entities {Study,Patient,Series, FrameOfReference, Equipment} will be the same across 
    /// a set of images related to a volume. Change this class so it is a collection of Images relating to the same
    /// series and make this knowledge explicit by sharing the higher level instances. 
    /// </summary>
    public class DicomIdentifiers
    {
        /// <summary>
        /// Study level information 
        /// </summary>
        public Dico