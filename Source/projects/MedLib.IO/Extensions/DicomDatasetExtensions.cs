///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace Dicom
{
    using System;
    using MedLib.IO.Extensions;
    using InnerEye.CreateDataset.Volumes;

    /// <summary>
    /// DICOM dataset extension methods for extracting attribute information.
    /// </summary>
    public static class DicomDatasetExtensions
    {
        /// <summary>
        /// Gets the value of the 'RescaleIntercept' attribute as a double.
        /// Note: This should only be used on CT datasets.
        /// </summary>
        /// <param name="dicomDataset">The DICOM dataset.</param>
        /// <returns>If the pixel representation is signed.</returns>
        /// <exception cref="ArgumentNullException">The provided DICOM dataset was null.</exception>
        /// <exception cref="ArgumentException">The provided DICOM dataset did not contain the 'RescaleIntercept' tag or was not a CT image.</exception>
        public static double GetRescaleIntercept(this DicomDataset dicomDataset)
        {
            CheckSopClass(dicomDataset, DicomUID.CTImageStorage);
            return dicomDataset.GetRequiredDicomAttribute<double>(DicomTag.RescaleIntercept);
        }

        /// <summary>
        /// Gets the value of the 'RescaleSlope' attribute as a double.
        /// Note: This should only be used on CT datasets.
        /// </summary>
        /// <param name="dicomDataset">The DICOM dataset.</param>
        /// <returns>If the pixel representation is signed.</returns>
        /// <exception cref="ArgumentNullException">The provided DICOM dataset was null.</exception>
        /// <exception cref="ArgumentException">The provided DICOM dataset did not contain the 'RescaleSlope' tag or was not a CT image.</exception>
        public static double GetRescaleSlope(this DicomDataset dicomDataset)
        {
            CheckSopClass(dicomDataset, DicomUID.CTImageStorage);
            return dicomDataset.GetRequiredDicomAttribute<double>(DicomTag.RescaleSlope);
        }

        /// <summary>
        /// Checks the SOP class of the provided DICOM dataset matches the expected DICOM UID.
        /// </summary>
        /// <param name="dicomDataset">The DICOM dataset to get the SOP class from.</param>
        /// <param name="dicomUID">The expected SOP class of the DICOM dataset.</param>
        /// <exception cref="ArgumentNullException">The DICOM dataset or DICOM UID is null.</exception>
        /// <exception cref="ArgumentException">The provided DICOM dataset did not match the expected SOP class.</exception>
        public static void CheckSopClass(this DicomDataset dicomDataset, DicomUID dicomUID)
        {
            dicomDataset = dicomDataset ?? throw new ArgumentNullException(nameof(dicomDataset));
            dicomUID = dicomUID ?? throw new ArgumentNullException(nameof(dicomUID));

            if (dicomDataset.GetSopClass() != dicomUID)
            {
                throw new ArgumentException("The provided DICOM dataset is not a CT image.", nameof(dicomDataset));
            }
        }

        /// <summary>
        /// Gets the value from the 'PixelRepresentation' attribute and checks if it equals 1.
        /// If 1, the underlying voxel information is signed.
        /// </summary>
        /// <param name="dicomDataset">The DICOM dataset.</param>
        /// <returns>If the pixel representation is signed.</returns>
        /// <exception cref="ArgumentNullException">The provided DICOM dataset was null.</exception>
        /// <exception cref="ArgumentException">The provided DICOM dataset did not contain the 'PixelRepresentation' tag.</exception>
        public static bool IsSignedPixelRepresentation(this DicomDataset dicomDataset)
        {
            dicomDataset = dicomDataset ?? throw new ArgumentNullException(nameof(dicomDataset));
            return dicomDataset.GetRequiredDicomAttribute<int>(DicomTag.PixelRepresentation) == 1;
        }

        /// <summary>
        /// Gets the high bit value from the DICOM dataset.
        /// </summary>
        /// <param name="dicomDataset">The DICOM dataset.</param>
        /// <returns>The high bit value.</returns>
        /// <exception cref="ArgumentNullException">The provided DICOM dataset was