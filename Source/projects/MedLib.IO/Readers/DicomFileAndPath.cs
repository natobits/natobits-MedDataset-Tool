///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿
namespace MedLib.IO.Readers
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using Dicom;

    /// <summary>
    /// Read only tuple of DicomFile and its original path
    /// </summary>
    public sealed class DicomFileAndPath
    {
        /// <summary>
        /// Creates a new instance of the class.
        /// </summary>
        /// <param name="dicomFile">The value to use fo