///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace MedLib.IO.Writers
{
    using Dicom;
    using Models.DicomRt;

    public class RtStructWriter
    {
        public static void SaveRtStruct(string filePath, RadiotherapyStruct rtStruct)
        {
            var file = GetRtStructFile(rtStruct);
            file.Save(filePath);
        }

        public static DicomFile GetRtStructFile(Radi