///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace MedLib.IO
{
    using System;
    using System.Threading.Tasks;

    /// <summary>
    /// Contains method for running parallel loops, that are optimized for running over large indexing ranges.
    /// </summary>
    public static class FastParallel
    {
        /// <summary>
        /// Get the starting index and end index (inclusive) when dividing a set of <paramref name="count"/> items
        /// into roughly equal sized batches (+- 1), and processing the batch with index given in <paramref name="currentBatch"/>.
        /// If there are more batches than items, return (0, -1) for the batches that have nothing to do.
        /// </summary>
        /// <param name="count">The total number of items to process. Valid indices are from 0 to (items - 1).</param>
        /// <param name="currentBatch">The currently processed batch. Valid batch numbers are 0 to 
        /// (totalBatches - 1).</param>
        /// <param name="totalBatches">The total number of batches.