///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

namespace InnerEye.CreateDataset.Math
{
    using System;
    using Volumes;
    using ImageProcessing;
    using Morphology;
    using System.Linq;

    public static class MorphologicalExtensions
    {
        /// <summary>
        /// Erode the input mask by the same margin in each dimension
        /// </summary>
        /// <param name="input">The volume is a binary mask where 1 represents foreground and 0 represents background</param>
        /// <param name="mmMargin">Erosion in the x,y and z dimension</param>
        /// <param name="structuringElement">Structuring element to use (the default implementation is an ellipsoid)</param>
        /// <returns>the eroded structure: value is 1 inside the structure, 0 outside.</returns>
        public static Volume3D<byte> Erode(this Volume3D<byte> volume, double mmMargin, StructuringElement structuringElement = null)
        {
            return DilateErode(volume, mmMargin, mmMargin, mmMargin, true, null, structuringElement);
        }

        /// <summary>
        /// Dilate the input mask by the same margin in each dimension, taking into account the restriction volume 
        /// </summary>
        /// <param name="input">The volume is a binary mask where 1 represents foreground and 0 represents background</param>
        /// <param name="mmMargin">Erosion in the x,y and z dimension</param>
        /// <param name="restriction">The restriction volume is optional and contrainst the dilation to another volume</param>
        /// <param name="structuringElement">Structuring element to use (the default implementation is an ellipsoid)</param>
        /// <returns>the dilated structure: value is 1 inside the structure, 0 outside.</returns>
        public static Volume3D<byte> Dilate(this Volume3D<byte> volume, double mmMargin,
            Volume3D<byte> restriction = null, StructuringElement structuringElement = null)
        {
            return DilateErode(volume, mmMargin, mmMargin, mmMargin, false, restriction, structuringElement);
        }

        /// <summary>
        /// Erode the input mask by the margins specified in each dimension
        /// </summary>
        /// <param name="input">The volume is a binary mask where 1 represents foreground and 0 represents background</param>
        /// <param name="mmMarginX">Erosion in the x dimension</param>
        /// <param name="mmMarginY">Erosion in the y dimension</param>
        /// <param name="mmMarginZ">Erosion in the z dimension</param>
        /// <param name="structuringElement">Structuring element to use (the default implementation is an ellipsoid)</param>
        /// <returns>the eroded structure: value is 1 inside the structure, 0 outside.</returns>
        public static Volume3D<byte> Erode(this Volume3D<byte> volume, double mmMarginX, double mmMarginY, double mmMarginZ, StructuringElement structuringElement = null)
        {
            return DilateErode(volume, mmMarginX, mmMarginY, mmMarginZ, true, null, structuringElement);
        }

        /// <summary>
        /// Dilate the input mask by the margins specified in each dimension, taking into account the restriction volume
        /// </summary>
        /// <param name="input">The volume is a binary mask where 1 represents foreground and 0 represents background</param>
        /// <param name="mmMarginX">Erosion in the x dimension</param>
        /// <param name="mmMarginY">Erosion in the y dimension</param>
        /// <param name="mmMarginZ">Erosion in the z dimension</param>
        /// <param name="restriction">The restriction volume is optional and contrainst the dilation to another volume</param>
        /// <param name="structuringElement">Structuring element to use for (the default implementation is an ellipsoid)</param>
        /// <returns>the dilated structure: value is 1 inside the structure, 0 outside.</returns>
        public static Volume3D<byte> Dilate(this Volume3D<byte> volume, double mmMarginX, double mmMarginY, double mmMarginZ,
            Volume3D<byte> restriction = null, StructuringElement structuringElement = null)
        {
            return DilateErode(volume, mmMarginX, mmMarginY, mmMarginZ, false, restriction, structuringElement);
        }


        /// <summary>
        /// Creates a new volume with the provided Dilation/Erosion margins applied
        /// The algorithm creates an ellipsoid structuring element (SE), extracts the surface poits of the ellipsoid, computes
        /// difference sets (see: StructuringElement.cs for further details) 
        /// and then paints the resulting volume on all the surface voxels.
        /// A connected components search is used to ensure the operation handles multiple components correctly
        /// </summary>
        /// <param name="input">The volume is a binary mask where 1 represents foreground and 0 represents background</param>
        /// <param name="mmMarginX">Dilation/Erosion in the x dimension</param>
        /// <param name="mmMarginY">Dilation/Erosion in the y dimension</param>
        /// <param name="mmMarginZ">Dilation/Erosion in the z dimension</param>
        /// <param name="isErosion">Only erosion or dilation can be performed at one time</param>
        /// <param name="restriction">The restriction volume is optional and contrainst the dilation to another volume</param>
        /// <param name="structuringElement">Structuring element to use (the default implementation is an ellipsoid)</param>
        /// <returns>the dilated-and-eroded structure: value is 1 inside the structure, 0 outside.</returns>
        private static Volume3D<byte> DilateErode(this Volume3D<byte> input, double mmMarginX, double mmMarginY,
            double mmMarginZ, bool isErosion, Volume3D<byte> restriction = null, StructuringElement structuringElement = null )
        {
            // Check input and restriction volume compatibility
            ValidateInputs(input, restriction, mmMarginX, mmMarginY, mmMarginZ);

            // Copy the input as only surface points are affected
            var result = input.Copy();

            // Calculate erosion/dilation bounds
            int xNumberOfPixels = (int)Math.Round(mmMarginX / input.SpacingX);
            int yNumberOfPixels = (int)Math.Round(mmMarginY / input.SpacingY);
            int zNumberOfPixels = (int)Math.Round(mmMarginZ / input.SpacingZ);

            // Check if there is nothing to do
            if (xNumberOfPixels == 0 && yNumberOfPixels == 0 && zNumberOfPixels == 0)
            {
                return result;
            }

            // The dimensions in which the operation will be performed in
            bool dilationRequiredInX = xNumberOfPixels > 0;
            bool dilationRequiredInY = yNumberOfPixels > 0;
            bool dilationRequiredInZ = zNumberOfPixels > 0;

            byte labelToPaint = ModelConstants.MaskBackgroundIntensity;

            // We do this as we always erode at least one surface point 
            if (isErosion)
            {
                xNumberOfPixels = xNumberOfPixels > 1 ? xNumberOfPixels - 1 : 0;
                yNumberOfPixels = yNumberOfPixels > 1 ? yNumberOfPixels - 1 : 0;
                zNumberOfPixels = zNumberOfPixels > 1 ? zNumberOfPixels - 1 : 0;
            }
            else
            {
                labelToPaint = ModelConstants.MaskForegroundIntensity;
            }

             // Create an ellipsoid structuring element (if none provided)
            var ellipsoidStructuringElement = structuringElement ?? 
                new StructuringElement(xNumberOfPixels, yNumberOfPixels, zNumberOfPixels);

            // Ensure we always paint at least one component fully for every component in the volume
            var components = PaintFullSEOnceForEachConnectedComponent(
                input, restriction, result,
                ellipsoidStructuringElement, dilationRequiredInX, dilationRequiredInY, dilationRequiredInZ, labelToPaint);

            // Check that components were found
            if (components > 0)
            {
                //We now march along the input from left to right on each slice and for each surface point on the volume paint
                //all of the surface points of the structuring element
                result.ParallelIterateSlices(p =>
                {
                    // Check that we are in a surface point on the volume
                    // This is to make sure that any dilation/erosion is performed around the edges of the components of the mask
                    if (input.IsSurfacePoint(p.x, p.y, p.z, dilationRequiredInX, dilationRequiredInY, dilationRequiredInZ))
                    {
                        ellipsoidStructuringElement.PaintSurfacePointsOntoVolume(result, restriction, labelToPaint, p.x, p.y, p.z);
                    }
                });
            }
            return result;
        }

        /// <summary>
        /// Paint all of the points that lie inside the SE mask for a single surface point on each of the components of the input image
        /// returns the num