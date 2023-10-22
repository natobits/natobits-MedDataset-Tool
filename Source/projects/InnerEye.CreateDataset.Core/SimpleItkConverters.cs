///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

﻿namespace InnerEye.CreateDataset.Core
{
    using System;
    using System.Runtime.InteropServices;
    using itk.simple;
    using InnerEye.CreateDataset.Volumes;

    /// <summary>
    /// Holds an ITK image, and the buffer that was used to create it when initializing the image from
    /// a managed <see cref="Volume3D{T}"/>. The class ensures that the memory is freed at the end of the object's lifetime.
    /// </summary>
    public class ItkImageFromManaged : IDisposable
    {
        /// <summary>
        /// Creates a new instance of the class, by cloning the given array and storing a pinned
        /// handle to the memory in the <see cref="Handle"/> property.
        /// </summary>
        /// <param name="array"></param>
        private ItkImageFromManaged(Array array)
        {
            Handle = GCHandle.Alloc(array.Clone(), GCHandleType.Pinned);
        }

        /// <summary>
        /// Gets the pinned handle to the image buffer that the present object stores. The handle can be null
        /// after calling dispose on the object.
        /// </summary>
        private GCHandle? Handle { get; set; }

        /// <summary>
        /// Gets the ITK image that is stored in the present object.
        /// </summary>
        public Image Image { get; private set; }

        /// <summary>
        /// Frees the memory that has been allocated for the image, and the ITK image itself.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Frees the memory that has been allocated for the image, and the ITK image itself.
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "disposing")]
        protected virtual void Dispose(bool disposing)
        {
            if (Handle != null)
            {
                Handle.Value.Free();
                Handle = null;
            }

            if (Image != null)
            {
                Image.Dispose();
                Image = null;
            }
        }

        /// <summary>
        /// Converts a volume to the corresponding SimpleITK image,
        /// preserving all voxel values and transformations. The resulting image 
        /// will have pixel type <see cref="PixelIDValueEnum.sitkUInt8"/>.
        /// </summary>
        /// <param name="volume">The volume that should be converted to an ITK image.</param>
        /// <returns></returns>

        public static ItkImageFromManaged FromVolume(Volume3D<byte> volume)
        {
            return FromVolume(volume, SimpleITK.ImportAsUInt8);
        }

        /// <summary>
        /// Converts the volume to the corresponding SimpleITK image,
        /// preserving all voxel values and transformations. The resulting image 
        /// will have pixel type <see cref="PixelIDValueEnum.sitkInt16"/>.
        /// </summary>
        /// <param name="volume">The volume that should be converted to an ITK image.</param>
        /// <returns></returns>
        public static ItkImageFromManaged FromVolume(Volume3D<short> volume)
        {
            return FromVolume(volume, SimpleITK.ImportAsInt16);
        }

        /// <summary>
        /// Converts the volume to the corresponding SimpleITK image,
        /// preserving all voxel values and transformations. The resulting image 
        /// will have pixel type <see cref="PixelIDValueEnum.sitkFloat32"/>.
        /// </summary>
        /// <param name="volume">The volume that should be converted to an ITK image.</param>
        /// <returns></returns>
        public static ItkImageFromManaged FromVolume(Volume3D<float> volume)
        {
            return FromVolume(volume, SimpleITK.ImportAsFloat);
        }

        /// <summary>
        /// Converts a volume to a SimpleITK Image, preserving all voxel values and transforms.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="volume">The volume to convert.</param>
        /// <param name="import">The SimpleITK import function to use when creating the image.
        /// Arguments are the voxel buffer, image dimensions, image spacing, image origin,
        /// image orientation.</param>
        /// <returns></returns>
        private static ItkImageFromManaged FromVolume<T>(Volume3D<T> volume,
            Func<IntPt