///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace MedLib.IO
{
    using Dicom;
    using MedLib.IO.Extensions;
    using InnerEye.CreateDataset.Volumes;
    using Models;
    using Models.DicomRt;
    using Readers;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Writers;
    using static MedLib.IO.NiftiIO.NiftiInternal;

    public struct VolumeLoaderResult
    {
        public VolumeLoaderResult(string seriesId, MedicalVolume volume, Exception error, IReadOnlyList<string> warnings)
        {
            SeriesUid = seriesId;
            Volume = volume;
            Error = error;
            Warnings = warnings;
        }

        /// <summary>
        /// The DICOM series UID that the volume belongs to
        /// </summary>
        public string SeriesUid { get; }

        /// <summary>
        /// The medical volume or null if Error != null
        /// </summary>
        public MedicalVolume Volume { get; }

        /// <summary>
        /// Contains the first exception that occurred attempting to load a volume => volume = null
        /// </summary>
        public Exception Error { get; }

        /// <summary>
        /// A list of warnings that occured loading the volume => volume != null
        /// </summary>
        public IReadOnlyList<string> Warnings { get; }
    }

    /// <summary>
    /// Contains methods to load and save different representations of medical volumes, working
    /// with Dicom, Nifti and HDF5 files.
    /// </summary>
    public class MedIO
    {
        /// <summary>
        /// The suffix for files that contain uncompressed Nifti data.
        /// </summary>
        public const string UncompressedNiftiSuffix = ".nii";

        /// <summary>
        /// The suffix for files that contain GZIP compressed Nifti data.
        /// </summary>
        public const string GZipCompressedNiftiSuffix = UncompressedNiftiSuffix + ".gz";

        /// <summary>
        /// The suffix for files that contain LZ4 compressed Nifti data.
        /// </summary>
        public const string LZ4CompressedNiftiSuffix = UncompressedNiftiSuffix + ".lz4";

        /// <summary>
        /// The suffix for files that contain uncompressed HDF5 data.
        /// </summary>
        public const string UncompressedHDF5Suffix = ".h5";

        /// <summary>
        /// The suffix for files that contain GZIP compressed HDF5 data.
        /// </summary>
        public const string GZipCompressedHDF5Suffix = UncompressedHDF5Suffix + ".gz";

        /// <summary>
        /// The suffix for files that contain SZIP compressed HDF5 data.
        /// </summary>
        public const string SZipCompressedHDF5Suffix = UncompressedHDF5Suffix + ".sz";

        /// <summary>
        /// Gets the type of compression that was applied to the given Nifti file,
        /// by looking at the file extension.
        /// If the given file name is neither a compressed nor an uncompressed Nifti file,
        /// return null.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static NiftiCompression? GetNiftiCompression(string path)
        {
            if (path.EndsWith(GZipCompressedNiftiSuffix))
            {
                return NiftiCompression.GZip;
            }
            if (path.EndsWith(LZ4CompressedNiftiSuffix))
            {
                return NiftiCompression.LZ4;
            }
            if (path.EndsWith(UncompressedNiftiSuffix))
            {
                return NiftiCompression.Uncompressed;
            }
            return null;
        }

        /// <summary>
        /// Returns true if the given file name identifies a Nifti file, either compressed 
        /// or uncompressed.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static bool IsNiftiFile(string fileName)
        {
            var compression = GetNiftiCompression(fileName);
            return compression != null;
        }

        /// <summary>
        /// Gets the type of compression that was applied to the given Nifti file,
        /// by looking at the file extension.
        /// If the given file name is neither a compressed nor an uncompressed Nifti file,
        /// an <see cref="ArgumentException"/> is thrown.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static NiftiCompression GetNiftiCompressionOrFail(string path)
        {
            var compression = GetNiftiCompression(path);
            if (compression == null)
            {
                throw new ArgumentException($"NIFTI filenames must end with '{UncompressedNiftiSuffix}' or '{GZipCompressedNiftiSuffix}' or '{LZ4CompressedNiftiSuffix}' (case sensitive), but got: {path}");
            }
            return compression.Value;
        }

        /// <summary>
        /// Gets the extension that a Nifti file should have when using the compression format
        /// given in the argument.
        /// </summary>
        /// <param name="compression"></param>
        /// <returns></returns>
        public static string GetNiftiExtension(NiftiCompression compression)
        {
            switch (compression)
            {
                case NiftiCompression.GZip:
                    return GZipCompressedNiftiSuffix;
                case NiftiCompression.LZ4:
                    return LZ4CompressedNiftiSuffix;
                case NiftiCompression.Uncompressed:
                    return UncompressedNiftiSuffix;
                default:
                    throw new ArgumentException($"Unsupported compression {compression}", nameof(compression));
            }
        }

        /// <summary>
        /// Expects path to point to a folder containing exactly 1 volume.
        /// </summary>
        /// <param name="path"></param>
        /// <param name="acceptanceTests"></param>
        /// <ret