///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace MedLib.IO.Readers
{
    using System;
    using System.Threading.Tasks;
    using Dicom;
    using Dicom.Imaging;
    using Dicom.Imaging.Codec;
    using MedLib.IO.Models;
    using InnerEye.CreateDataset.Volumes;

    /// <summary>
    /// DICOM reader class for decoding pixel data from a collection of DICOM datasets.
    /// </summary>
    public static class DicomSeriesImageReader
    {
        /// <summary>
        /// Builds a 3-dimensional volume from the provided volume information.
        /// This method will parallelise voxel extraction per slice.
        /// </summary>
        /// <param name="volumeInformation">The volume information.</param>
        /// <param name="maxDegreeOfParallelism">The maximum degrees of parallelism when extracting voxel data from the DICOM datasets.</param>
        /// <returns>The 3-dimensional volume.</returns>
        /// <exception cref="ArgumentNullException">The provided volume information was null.</exception>
        /// <exception cref="InvalidOperationException">The decoded DICOM pixel data was not the expected length.</exception>
        public static Volume3D<short> BuildVolume(
            VolumeInformation volumeInformation,
            uint maxDegreeOfParallelism = 100)
        {
            volumeInformation = volumeInformation ?? throw new ArgumentNullException(nameof(volumeInformation));

            // Allocate the array for reading the volume data.
            var result = new Volume3D<short>(
                (int)volumeInformation.Width,
                (int)volumeInformation.Height,
                (int)volumeInformation.Depth,
                volumeInformation.VoxelWidthInMillimeters,
                volumeInformation.VoxelHeightInMillimeters,
                volumeInformation.VoxelDepthInMillimeters,
                volumeInformation.Origin,
                volumeInformation.Direction);

            Parallel.For(
                0,
                volumeInformation.Depth,
                new ParallelOptions() { MaxDegreeOfParallelism = (int)maxDegreeOfParallelism },
                i => WriteSlice(result, volumeInformation.GetSliceInformation((int)i), (uint)i));

            return result;
        }

        /// <summary>
        /// Gets the uncompressed pixel data from the provided DICOM dataset.
        /// </summary>
        /// <param name="dataset">The DICOM dataset.</param>
        /// <returns>The uncompressed pixel data as a byte array.</returns>
        /// <exception cref="ArgumentNullException">The provided DICOM dataset was null.</exception>
        public static byte[] GetUncompressedPixelData(DicomDataset dataset)
        {
            dataset = dataset ?? throw new ArgumentNullException(nameof(dataset));

            if (dataset.InternalTransferSyntax.IsEncapsulated)
            {
                // Decompress single frame from source dataset
                var transcoder = new DicomTranscoder(
                    inputSyntax: dataset.InternalTransferSyntax,
                    outputSyntax: DicomTransferSyntax.ExplicitVRLittleEndian);

                return transcoder.DecodeFrame(dataset, 0).Data;
            }
            else
            {
                // Pull uncompressed frame from source pixel data
                var pixelData = DicomPixelData.Create(dataset);
                return pixelData.GetFrame(0).Data;
            }
        }

        /// <summary>
        /// Writes a slice into the 3-dimensional volume based on the slice information and slice index provided.
        /// </summary>
        /// <param name="volume">The 3-dimensional volume.</param>
        /// <param name="sliceInformation">The slice information.</param>
        /// <param name="sliceIndex">The slice index the slice information relates to.</param>
        /// <exception cref="ArgumentException">The provided slice index was outside the volume bounds.</exception>
        /// <exception cref="InvalidOperationException">The decoded DICOM pixel data was not the expected length.</exception>
        private static unsafe void WriteSlice(Volume3D<short> volume, SliceInformation sliceInformation, uint sliceIndex)
        {
            // Check the provided slice index exists in t