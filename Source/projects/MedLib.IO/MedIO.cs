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
        /// <returns></returns>
        public static async Task<MedicalVolume> LoadSingleDicomSeriesAsync(string path, IVolumeGeometricAcceptanceTest acceptanceTests)
        {
            var attributes = File.GetAttributes(path);

            if ((attributes & FileAttributes.Directory) != FileAttributes.Directory)
            {
                throw new ArgumentException("Folder path was expected.");
            }

            var results = await LoadAllDicomSeriesInFolderAsync(path, acceptanceTests);

            if (results.Count != 1)
            {
                throw new Exception("Folder contained multiple series.");
            }

            if (results[0].Error != null)
            {
                throw new Exception("Error loading DICOM series.", results[0].Error);
            }

            return results[0].Volume;
        }

        /// <summary>
        /// Loads a medical volume from a Nifti file. The <see cref="MedicalVolume.Volume"/> property
        /// will be set to the volume in the Nifti file, the RT structures will be empty, empty
        /// Dicom identifiers.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static MedicalVolume LoadMedicalVolumeFromNifti(string path)
        {
            var volume = LoadNiftiAsShort(path);

            return new MedicalVolume(
                volume,
                new DicomIdentifiers[0],
                new[] { path },
                RadiotherapyStruct.CreateDefault(new[] { DicomIdentifiers.CreateEmpty() }));
        }

        /// <summary>
        /// Loads a medical volume from a Nifti file. The <see cref="MedicalVolume.Volume"/> property
        /// will be set to the volume in the Nifti file, the RT structures will be empty, empty
        /// Dicom identifiers.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static async Task<MedicalVolume> LoadMedicalVolumeFromNiftiAsync(string path)
        {
            return await Task.Run(() => LoadMedicalVolumeFromNifti(path));
        }

        public static Tuple<RadiotherapyStruct, string> LoadStruct(string rtfile, Transform3 dicomToData, string studyUId, string seriesUId)
        {
            try
            {
                var file = DicomFile.Open(rtfile);
                return RtStructReader.LoadContours(file.Dataset, dicomToData, seriesUId, studyUId, true);
            }
            catch (Exception ex)
            {
                throw new ArgumentException($"RT file {rtfile} cannot be loaded - {ex.Message}");
            }
        }

        /// <summary>
        /// Analyse all DICOM files in the given folder and attempt to construct a volume for the given seriesUID
        /// </summary>
        /// <param name="pathFolder">The absolute path to the folder containing the DICOM files</param>
        /// <param name="seriesUID">The DICOM Series UID you wish to load</param>
        /// <param name="acceptanceTests">An implementation of IVolumeGeometricAcceptanceTest defining the geometric constraints of your application</param>
        /// <param name="loadStructuresIfExists">True if rt-structures identified in the folder and referencing seriesUID should be loaded</param>
        /// <param name="supportLossyCodecs">If you wish to accept lossy encodings of image pixel data</param>
        /// <returns></returns>
        public static async Task<VolumeLoaderResult> LoadDicomSeriesInFolderAsync(
            string pathFolder, string seriesUID, IVolumeGeometricAcceptanceTest acceptanceTests, bool loadStructuresIfExists = true, bool supportLossyCodecs = true)
        {
            var dfc = await DicomFileSystemSource.Build(pathFolder);
            var pSeriesUID = DicomUID.Parse(seriesUID);

            return LoadDicomSeries(dfc, pSeriesUID, acceptanceTests, loadStructuresIfExists, supportLossyCodecs);
        }

        /// <summary>
        /// Analyse all DICOM files in the given folder and attempt to construct all volumes for CT and MR series therein.
        /// </summary>
        /// <param name="pathFolder">The absolute path to the folder containing the DICOM files</param>
        /// <param name="acceptanceTests">An implementation of IVolumeGeometricAcceptanceTest defining the geometric constraints of your application</param>
        /// <param name="loadStructuresIfExists">True if rt-structures identified in the folder and referencing a volume should be loaded</param>
        /// <param name="supportLossyCodecs">If you wish to accept lossy encodings of image pixel data</param>
        /// <returns>A list of volume loading results for the specified folder</returns>
        public static async Task<IList<VolumeLoaderResult>> LoadAllDicomSeriesInFolderAsync(
            string pathFolder, IVolumeGeometricAcceptanceTest acceptanceTests, bool loadStructuresIfExists = true, bool supportLossyCodecs = true)
        {
            var stopwatch = Stopwatch.StartNew();
            var dfc = await DicomFileSystemSource.Build(pathFolder);
            stopwatch.Stop();
            Trace.TraceInformation($"Analysing folder structure took: {stopwatch.ElapsedMilliseconds} ms");

            return LoadAllDicomSeries(dfc, acceptanceTests, loadStructuresIfExists, supportLossyCodecs);
        }

        /// <summary>
        /// Attempt to load all volume for all CT and MR image series within the given DicomFolderContents
        /// </summary>
        /// <param name="dfc">A pre-built description of DICOM contents within a particular folder</param>
        /// <param name="acceptanceTests">An implementation of IVolumeGeometricAcceptanceTest defining the geometric constraints of your application</param>
        /// <param name="loadStructuresIfExists">True if rt-structures identified in the folder and referencing a volume should be loaded</param>
        /// <param name="supportLossyCodecs">If you wish to accept lossy encodings of image pixel data</param>
        /// <returns></returns>
        public static IList<VolumeLoaderResult> LoadAllDicomSeries(
            DicomFolderContents dfc, IVolumeGeometricAcceptanceTest acceptanceTests, bool loadStructuresIfExists, bool supportLossyCodecs)
        {
            var stopwatch = Stopwatch.StartNew();

            var resultList = new List<VolumeLoaderResult>();

            foreach (var s in dfc.Series)
            {
                if (s.SeriesUID != null)
                {
                    resultList.Add(LoadDicomSeries(dfc, s.SeriesUID, acceptanceTests, loadStructuresIfExists, supportLossyCodecs));
                }
            }

            stopwatch.Stop();
            Trace.TraceInformation($"Reading all DICOM series took: {stopwatch.ElapsedMilliseconds} ms");
            return resultList;
        }

        /// <summary>
        /// Loads a Nifti file from disk, returning it as a <see cref="Volume3D{T}"/> with datatype
        /// <see cref="byte"/>, irrespective of the datatype used in the Nifti file itself.
        /// </summary>
        /// <param name="path">The file to load.</param>
        /// <returns></returns>
        public static Volume3D<byte> LoadNiftiAsByte(string path)
        {
            return LoadNiftiFromFile(path, NiftiIO.ReadNiftiAsByte);
        }

        /// <summary>
        /// Loads a Nifti file from disk, where the Nifti file is expected to have
        /// voxels in 'byte' format.
        /// </summary>
        /// <param name="path">The file to load.</param>
        /// <returns></returns>
        public static Volume3D<byte> LoadNiftiInByteFormat(string path)
        {
            return LoadNiftiFromFile(path, NiftiIO.ReadNiftiInByteFormat);
        }

        /// <summary>
        /// Loads a Nifti file from disk, returning it as a <see cref="Volume3D{T}"/> with datatype
        /// <see cref="short"/>, irrespective of the datatype used in the Nifti file itself.
        /// </summary>
        /// <param name="path">The file to load.</param>
        /// <returns></returns>
        public static Volume3D<short> LoadNiftiAsShort(string path)
  