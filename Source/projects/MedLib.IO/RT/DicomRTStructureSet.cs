///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------


namespace MedLib.IO.RT
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    using Dicom;

    using Extensions;
    using Readers;

    public class DicomRTStructureSet
    {
        /// <summary>
        /// Short string(16 chars) Type 1. 
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Long string (64 chars) Type 3
        /// </summary>
        public string Name { get; set;  }

        /// <summary>
        /// Optional description of the structure set. Type 3. Short Text (max 1024 chars)
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Date when structure set was last modified. Type 2.
        /// </summary>
        public string Date { get; private set; }

        /// <summary>
        /// Time when structure set was last modified. Type 2.
        /// </summary>
        public string Time { get; private set; }

        /// <summary>
        /// Referenced study/series/image sequence. Type 3.
        /// </summary>
        public IReadOnlyList<DicomRTFrameOFReference> ReferencedFramesOfRef { get; }

        private const string _dicomTimeFormat = "HHmmss";
        private const string _dicomDateFormat = "yyyyMMdd";
        private const string _hdf5TimeFormat = "HH:mm:ss";
        private const string _hdf5DateFormat = "yyyy-MM-ddTHH:mm:ss";

        public DicomRTStructureSet(
            string label,
            string name,
            string description,
            string date,
            string time,
            IReadOnlyList<DicomRTFrameOFReference> referencedFramesOfRef)
        {
            Label = label;
            Name = name;
            Description = description;
            Date = date;
            Time = time;
            ReferencedFramesOfRef = referencedFramesOfRef;
        }

        public static DicomRTStructureSet Read(DicomDataset ds)
        {
            var label = ds.GetTrimmedStringOrEmpty(DicomTag.StructureSetLabel);
            var name = ds.GetTrimmedStringOrEmpty(DicomTag.StructureSetName);
            var description = ds.GetTrimmedStringOrEmpty(DicomTag.StructureSetDescription);

            var date = ds.GetStringOrEmpty(DicomTag.StructureSetDate);
            var time = ds.GetStringOrEmpty(DicomTag.StructureSetTime);

            var frames = new List<DicomRTFrameOFReference>();

            if (!ds.Contains(DicomTag.ReferencedFrameOfReferenceSequence))
            {
                return new DicomRTStructureSet(label, name, description, date, time, frames);
            }

            var seq = ds.GetSequence(DicomTag.ReferencedFrameOfReferenceSequence);

            frames.AddRange(seq.Select(DicomRTFrameOFReference.Read));

            return new DicomRTStructureSet(label, name, description, date, time, frames);
        }

        public static void Write(DicomDataset ds, DicomRTStructureSet structureSet)
        {
            ds.Add(DicomTag.StructureSetLabel, structureSet.Label);
            ds.Add(DicomTag.StructureSetName, structureSet.Name);
            ds.Add(DicomTag.StructureSetDescription, structureSet.Description);
            ds.Add(DicomTag.StructureSetDate, structureSet.Date);
            ds.Add(DicomTag.StructureSetTime, structureSet.Time);

            var listOfFrames = structureSet.ReferencedFramesOfRef.Select(DicomRTFrameOFReference.Write).ToList();

            if (listOfFrames.Count > 0)
            {
                ds.Add(new DicomSequence(DicomTag.ReferencedFrameOfReferenceSequence, listOfFrames.ToArray()));
            }
        }


        public static DicomRTStructureSet CreateDefault(IReadOnlyList<DicomIdentifiers> identifiers)
        {
            var now = DateTime.UtcNow;

            return new DicomRTStructureSet(
                label: "unknown",
                name: string.Empty,
                description: string.Empty,
                date: now.ToString(_dicomDateFormat, CultureInfo.InvariantCulture),
                time: now.ToString(_dicomTimeFormat, CultureInfo.InvariantCulture),
                referencedFramesOfRef: new List <DicomRTFrameOFReference> { CreateReferencedFrames(identifiers) });
        }

        public static DicomRTStructureSet CreateDefaultFromHdf5()
        {
            var now = DateTime.UtcNow;

            return new DicomRTStructureSet(
                label: "unknown",
                name: string.Empty,
                description: string.Empty,
                date: now.ToString(_hdf5DateFormat, CultureInfo.InvariantCulture),
                time: now.ToString(_dicomTimeFormat, CultureInfo.InvariantCulture),
                referencedFramesOfRef: new List<DicomRTFrameOFReference>());
        }

        public void SetDateTime(DateTime dateTime)
        {
            Date = dateTime.ToString(_dicomDateFormat, CultureInfo.InvariantCulture);
            Time = dateTime.ToString(_dicomTimeFormat, CultureInfo.InvariantCulture);
        }

        public static DicomRTFrameOFReference CreateReferencedFrames(IReadOnlyList<DicomIdentifiers> identifiers)
        {
            // Check all identifiers have the same SeriesInstanceUid
            var firstIdentifier = identifiers.First();

            if (identifiers.Any(x => x.Series.SeriesInstanceUid != firstIdentifier.Series.SeriesInstanceUid))
            {
                throw new InvalidOperationException("the list of slices are not for the same