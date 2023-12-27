///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace MedLib.IO
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Numerics;
    using System.Threading.Tasks;

    using Dicom;
    using Dicom.IO.Buffer;

    using MedLib.IO.Models;
    using MedLib.IO.Models.DicomRt;
    using MedLib.IO.Readers;
    using MedLib.IO.RT;
    using MedLib.IO.Writers;

    using InnerEye.CreateDataset.Contours;
    using InnerEye.CreateDataset.Volumes;

    using MoreLinq;

    public enum ImageModality
    {
        /// <summary>
        /// Computer Tomography image modality.
        /// </summary>
        CT,

        /// <summary>
        /// Magnetic Resonance image modality.
        /// </summary>
        MR,
    }

    /// <summary>
    /// Helper class for converting Nii to Dicom. 
    /// Use with extreme care - many Dicom elements have to be halluzinated here, and there's no 
    /// guarantee that the resulting Dicom will be usable beyond what is needed in InnerEye.
    /// </summary>
    public static class NiiToDicomHelpers
    {
        /// <summary>
        /// The maximum number of characters in a string that is valid in a Dicom Long String element, see
        /// http://dicom.nema.org/dicom/2013/output/chtml/part05/sect_6.2.html
        /// </summary>
        public const int MaxDicomLongStringLength = 64;

        /// <summary>
        /// Converts from channeldId to ImageModality. Any channel name 'CT' (case insensitve) is
        /// recognized as CT, all other channel names as MR.
        /// </summary>
        /// <param name="channelId">The channel identifier.</param>
        /// <returns></returns>
        public static ImageModality InferModalityFromChannelId(string channelId)
        {
            if (channelId.Equals("ct", StringComparison.InvariantCultureIgnoreCase))
            {
                return ImageModality.CT;
            }

            // If not CT, assume MR
            return ImageModality.MR;
        }

        /// <summary>
        /// Creates a set of Dicom items that contain information about the manufacturer of the scanner,
        /// and other information.
        /// </summary>
        /// <param name="manufacturer">The value to use for the DicomTag.Manufacturer element.</param>
        /// <param name="studyId">The value to use for the DicomTag.StudyID tag</param>
        /// <param name="patientName">The value to use for the DicomTag.PatientName tag.</param>
        /// <returns></returns>
        public static DicomDataset DatasetWithExtraInfo(string manufacturer = null,
            string studyId = null,
            string patientName = null)
        {
            if (!IsValidDicomLongString(manufacturer))
            {
                throw new ArgumentException("The manufacturer is not a valid Dicom Long String.", nameof(manufacturer));
            }

            if (!IsValidDicomLongString(patientName))
            {
                throw new ArgumentException("The patient name is not a valid Dicom Long String.", nameof(patientName));
            }

            return new DicomDataset()
            {
                { new DicomUniqueIdentifier(DicomTag.Manufacturer, manufacturer ?? string.Empty) },
                { new DicomUniqueIdentifier(DicomTag.StudyID, studyId ?? string.Empty) },
                { new DicomUniqueIdentifier(DicomTag.PatientName, patientName ?? string.Empty) }
            };
        }

        /// <summary>
        /// Creates a Dicom UID for internal use only.
        /// The reason why we are not using fo-dicom uid creation code is because it is very slow.
        /// </summary>
        /// <returns></returns>
        public static DicomUID CreateUID()
        {
            return new DicomUID(GuidToUidStringUsingStringAndParse(Guid.NewGuid()), string.Empty, DicomUidType.Unknown);
        }

        /// <summary>
        /// Converts a volume 3D into a collection of Dicom files (split by slice on the primary plane).
        /// This code writes the patient position as HFS (this might not be correct but was needed at some point to view the output).
        /// 
        /// Note: This code has not been tested with MR data. It also assumes the Photometric Interpretation to be MONOCHROME2.
        /// Use with extreme care - many Dicom elements have to be halluzinated here, and there's no 
        /// guarantee that the resulting Dicom will be usable beyond what is needed in InnerEye.
        /// </summary>
        /// <param name="volume">The volume to convert.</param>
        /// <param name="modality">The image modality.</param>
        /// <param name="seriesDescription">The value to use as the Dicom series description.</param>
        /// <param name="patientID">The patient ID that should be used in the Dicom files. If null,
        /// a randomly generated patient ID will be used.</param>
        /// <param name="studyInstanceID">The study ID that should be used in the Dicom files (DicomTag.StudyInstanceUID). If null,
        /// a randomly generated study ID will be used.</param>
        /// <param name="additionalDicomItems">Additional Dicom items that will be added to each of the slice datasets. This can
        /// be used to pass in additional information like manufacturer.</param>
        /// <returns>The collection of Dicom files that represents the Dicom image series.</returns>
        public static IEnumerable<DicomFile> Convert(Volume3D<short> volume,
            ImageModality modality,
            string seriesDescription = null,
            string patientID = null,
            string studyInstanceID = null,
            DicomDataset additionalDicomItems = null)
        {
            seriesDescription = seriesDescription ?? string.Empty;
            patientID = CreateUidIfEmpty(patientID);
            studyInstanceID = CreateUidIfEmpty(studyInstanceID);

            if (!IsValidDicomLongString(seriesDescription))
            {
                throw new ArgumentException("The series description is not a valid Dicom Long String.", nameof(seriesDescription));
            }

            if (!IsValidDicomLongString(patientID))
            {
                throw new ArgumentException("The patient ID is not a valid Dicom Long String.", nameof(patientID));
            }

            if (!IsValidDicomLongString(studyInstanceID))
            {
                throw new ArgumentException("The study instance ID is not a valid Dicom Long String.", nameof(studyInstanceID));
            }

            var spacingZ = volume.SpacingZ;
            var imageOrientationPatient = new decimal[6];

            var directionColumn1 = volume.Direction.Column(0);
            var directionColumn2 = volume.Direction.Column(1);

            imageOrientationPatient[0] = (decimal)directionColumn1.X;
            imageOrientationPatient[1] = (decimal)directionColumn1.Y;
            imageOrientationPatient[2] = (decimal)directionColumn1.Z;
            imageOrientationPatient[3] = (decimal)directionColumn2.X;
            imageOrientationPatient[4] = (decimal)directionColumn2.Y;
            imageOrientationPatient[5] = (decimal)directionColumn2.Z;

            var frameOfReferenceUID = CreateUID().UID;
            var seriesUID = CreateUID().UID;
            var sopInstanceUIDs = new DicomUID[volume.DimZ];

            // DicomUID.Generate() is not thread safe. We must create unique DicomUID's single threaded.
            // https://github.com/fo-dicom/fo-dicom/issues/546
            for (var i = 0; i < sopInstanceUIDs.Length; i++)
            {
                sopInstanceUIDs[i] = CreateUID();
            }

            var results = new DicomFile[volume.DimZ];
            Parallel.For(0, volume.DimZ, i =>
            {
                var sliceLocation = (i * spacingZ) + volume.Origin.Z;
                var imagePositionPatient = volume.Transform.DataToDicom.Transform(new Point3D(0, 0, i));

                var dataset = new DicomDataset()
                {
                    { DicomTag.ImageType, new[] {"DERIVED", "PRIMARY", "AXIAL" } },
                    { DicomTag.PatientPosition, "HFS" },
                    { new DicomOtherWord(DicomTag.PixelData, new MemoryByteBuffer(ExtractSliceAsByteArray(volume, i))) },
                    { new DicomUniqueIdentifier(DicomTag.SOPInstanceUID, sopInstanceUIDs[i]) },
                    { new DicomUniqueIdentifier(DicomTag.SeriesInstanceUID, seriesUID) },
                    { new DicomUniqueIdentifier(DicomTag.PatientID, patientID) },
                    { new DicomUniqueIdentifier(DicomTag.StudyInstanceUID, studyInstanceID) },
                    { new DicomUniqueIdentifier(DicomTag.FrameOfReferenceUID, frameOfReferenceUID) },
                    { new DicomLongString(DicomTag.SeriesDescription, seriesDescription) },
                    { new DicomUnsignedShort(DicomTag.Columns, (ushort)volume.DimX) },
                    { new DicomUnsignedShort(DicomTag.Rows, (ushort)volume.DimY) },
                    { new DicomDecimalString(DicomTag.PixelSpacing, (decimal)volume.SpacingY, (decimal)volume.SpacingX) }, // Note: Spacing X & Y are not the expected way around
                    { new DicomDecimalString(DicomTag.ImagePositionPatient, (decimal)imagePositionPatient.X, (decimal)imagePositionPatient.Y, (decimal)imagePositionPatient.Z) },
                    { new DicomDecimalString(DicomTag.ImageOrientationPatient, imageOrientationPatient) },
                    { new DicomDecimalString(DicomTag.SliceLocation, (decimal)sliceLocation) },
                    { new DicomUnsignedShort(DicomTag.SamplesPerPixel, DicomSeriesInformationValidator.ExpectedSamplesPerPixel) },
                    { new DicomUnsignedShort(DicomTag.PixelRepresentation, 1) },
                    { new DicomUnsignedShort(DicomTag.BitsStored, DicomSeriesInformationValidator.ExpectedBitsAllocated) },
                    { new DicomUnsignedShort(DicomTag.BitsAllocated, DicomSeriesInformationValidator.ExpectedBitsAllocated) },
                    { new DicomUnsignedShort(DicomTag.HighBit, DicomSeriesInformationValidator.ExpectedBitsAllocated - 1) },
                    { new DicomCodeString(DicomTag.PhotometricInterpretation, DicomSeriesInformationValidator.ExpectedPhotometricInterpretation) }
                };

                if (modality == ImageModality.CT)
                {
                    dataset.Add(DicomTag.SOPClassUID, DicomUID.CTImageStorage);
                    dataset.Add(DicomTag.Modality, ImageModality.CT.ToString());

                    dataset.Add(new DicomItem[]
                    {
                        new DicomDecimalString(DicomTag.RescaleIntercept, 0),
                        new DicomDecimalString(DicomTag.RescaleSlope, 1),
                    });
                }
                else if (modality == ImageModality.MR)
                {
                    dataset.Add(DicomTag.SOPClassUID, DicomUID.MRImageStorage);
                    dataset.Add(DicomTag.Modality, ImageModality.MR.ToString());
                }

                if (additionalDicomItems != null)
                {
                    additionalDicomItems
                    .Clone()
                    .ForEach(item =>
                    {
                        if (!dataset.Contains(item.Tag))
                        {
                            dataset.Add(item);
                        }
                    });
                }

                results[i] = new DicomFile(dataset);
            });

            return results;
        }

        /// <summary>
        /// Saves a medical scan to a set of Dicom files. Each file is saved into a memory stream.
        /// The returned Dicom files have the Path property set to {sliceIndex}.dcm.
        /// Use with extreme care - many Dicom elements have to be halluzinated here, and there's no 
        /// guarantee that the resulting Dicom will be usable beyond what is needed in InnerEye.
        /// </summary>
        /// <param name="scan">The medical scan.</param>
        /// <param name="imageModality">The image modality through which the scan was acquired.</param>
        /// <param name="seriesDescription">The series description that should be used in the Dicom files.</param>
        /// <param name="patientID">The patient ID that should be used in the Dicom files. If null,
        /// a randomly generated patient ID will be used.</param>
        /// <param name="studyInstanceID">The study ID that should be used in the Dicom files (DicomTag.StudyInstanceUID). If null,
        /// a randomly generated study ID will be used.</param>
        /// <param name="additionalDicomItems">Additional Dicom items that will be added to each of the slice datasets. This can
        /// be used to pass in additional information like manufacturer.</param>
        /// <returns></returns>
        public static List<DicomFileAndPath> ScanToDicomInMemory(Volume3D<short> scan,
            ImageModality imageModality,
            string seriesDescription = null,
            string patientID = null,
            string studyInstanceID = null,
            DicomDataset additionalDicomItems = null)
        {
            var scanAsDicomFiles = Convert(scan, imageModality, seriesDescription, patientID, studyInstanceID, additionalDicomItems).ToList();
            var dicomFileAndPath = new List<DicomFileAndPath>();
            for (var index = 0; index < scanAsDicomFiles.Count; index++)
            {
                var stream = new MemoryStream();
                scanAsDicomFiles[index].Save(stream);
                stream.Seek(0, SeekOrigin.Begin);
                var dicomFile = DicomFileAndPath.SafeCreate(stream, $"{index}.dcm");
                dicomFileAndPath.Add(dicomFile);
            }
            return dicomFileAndPath;
        }

        /// <summary>
        /// Loads a medical volume from a set of Dicom files, including the RT structures if present.
        /// The Dicom files are expected to contain a single Dicom Series.
        /// </summary>
        /// <param name="dicomFileAndPath">The Dicom files to load from</param>
        /// <param name="maxPixelSizeRatioMR">The maximum allowed aspect ratio for pixels, if the volume is an MR scan.</param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException">If the volume cannot be loaded from the Dicom files</exception>
        public static MedicalVolume MedicalVolumeFromDicom(IReadOnlyList<DicomFileAndPath> dicomFileAndPath,
            double maxPixelSizeRatioMR = NonStrictGeometricAcceptanceTest.DefaultMaxPixelSizeRatioMR)
        {
            var acceptanceTest = new NonStrictGeometricAcceptanceTest("Non Square pixels", "Unsupported Orientation", maxPixelSizeRatioMR);
            var volumeLoaderResult = MedIO.LoadAllDicomSeries(
                                    DicomFolderContents.Build(dicomFileAndPath),
                                    acceptanceTest,
                                    loadStructuresIfExists: true,
                                    supportLossyCodecs: false);
            if (volumeLoaderResult.Count != 1)
            {
                throw new InvalidDataException($"Unable to load the scan from the Dicom files: There should be exactly 1 series, but got {volumeLoaderResult.Count}.");
            }

            var result = volumeLoaderResult[0];
            if (result.Volume == null)
            {
                throw new InvalidDataException("An exception was thrown trying to load the scan from the Dicom files.", result.Error);
            }

            return result.Volume;
        }

        /// <summary>
        /// Loads a medical volume from a set of Dicom files that live in the given folder.
        /// The Dicom loading will include the RT structures if present.
        /// The folder is expected to contain a single Dicom Series.
        /// </summary>
        /// <param name="folderWithDicomFiles">The folder that contains the Dicom files.</param>
        /// <returns></returns>
        /// <exception cref="InvalidDataException">If the volume cannot be loaded from the Dicom files</exception>
        public static MedicalVolume MedicalVolumeFromDicomFolder(string folderWithDicomFiles)
        {
            var filesOnDisk =
                Directory.EnumerateFiles(folderWithDicomFiles)
                .Select(DicomFileAndPath.SafeCreate)
                .ToList();
            return MedicalVolumeFromDicom(filesOnDisk);
        }

        /// <summary>
        /// Creates a radiotherapy structure from a set of contours, including the given rendering 
        /// information (name of the contour, color to render in).
        /// </summary>
        /// <param name="contours">The contours and their rendering information.</param>
        /// <param name="dicomIdentifiers">The Dicom identifiers for the scan to which the contours belong.</param>
        /// <param name="volumeTransform">The Dicom-to-data transformation of the scan to which the contours belong.</param>
        /// <returns></returns>
        public static RadiotherapyStruct ContoursToRadiotherapyStruct(IEnumerable<ContourRenderingInformation> contours,
            IReadOnlyList<DicomIdentifiers> dicomIdentifiers,
            VolumeTransform volumeTransform)
        {
            var radiotherapyStruct = RadiotherapyStruct.CreateDefault(dicomIdentifiers);

            int roiNumber = 0;
            var nameConverter = new DicomPersonNameConverter("InnerEye", "CreateDataset", string.Empty, string.Empty, string.Empty);
            foreach (var contour in contours)
            {
                // ROIs need to start at 1 by DICOM spec
                roiNumber++;
                // Create contours - mapping each contour into the volume. 
                var radiotherapyContour = RTStructCreator.CreateRadiotherapyContour(
                    contour.Contour,
                    dicomIdentifiers,
                    volumeTransform,
                    contour.Name,
                    (contour.Color.R, contour.Color.G, contour.Color.B),
                    roiNumber.ToString(),
                    nameConverter,
                    ROIInterpretedType.None
                    );
                radiotherapyStruct.Contours.Add(radiotherapyContour);
            }
            return radiotherapyStruct;
        }

        /// <summary>
        /// Creates a single Dicom file that contains a radiotherapy structure,
        /// derived from a set of contours and rendering information.
        /// </summary>
        /// <param name="contours">The contours and their rendering information.</param>
        /// <param name="dicomIdentifiers">The Dicom identifiers for the scan to which the contours belong.</param>
        /// <param name="