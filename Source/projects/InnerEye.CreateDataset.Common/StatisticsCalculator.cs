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
         