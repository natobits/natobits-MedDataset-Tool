///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace MedLib.IO
{
    using System;
    using InnerEye.CreateDataset.Contours;

    /// <summary>
    /// Contains a segmentation as a contour, and information about how it should be rendered within a Dicom file.
    /// </summary>
    public class ContourRenderingInformation
    {
        /// <summary>
        /// Creates a new instance of the class, setting all properties that the class holds.
        /// </summary>
        /// <param name="name">The name of the anatomical structure that is represented by the contour.</param>
        /// <param name="color">The color that should be used to render the contour.</param>
        /// <param name="contour">The contours broken down by slice of the scan.</param>
        /// <exception cref="ArgumentNullException">The contour name or mask was null.</exception>
        public ContourRenderingInf