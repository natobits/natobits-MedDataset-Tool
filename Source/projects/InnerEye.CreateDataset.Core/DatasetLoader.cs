///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿using MedLib.IO;
using MedLib.IO.Readers;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System;
using MedLib.IO.Models;

namespace InnerEye.CreateDataset.Core
{
    class DatasetLoader
    {
        private string _datasetPath;

        public DatasetLoader(string datasetPath)
        {
            _datasetPath = datasetPath;
        }

        ///