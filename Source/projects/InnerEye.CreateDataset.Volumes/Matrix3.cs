///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

namespace InnerEye.CreateDataset.Volumes
{
    using System;
    using System.Linq;

    /// <summary>
    /// Encodes a 3x3 matrix over double precision numbers
    /// </summary>
    [Serializable]
    public class Matrix3
    {
        /// <summary>
        /// Construct the zero matrix
        /// </summary>
        public Matrix3()
        {
            Data = new double[9];
        }

        /// <summary>
        /// Copy the given matrix by value.
        /// </summary>
        /// <param name="matrix"></param>
        public Matrix3(Matrix3 matrix)
        {
            Data = new double[9];
            Array.Copy(matrix.Data, Data, Data.Length);
        }

        /// <summary>
        /// Copy the given data into a matrix. data is accessed by [i*3 + j] where i is the column index and j the row index. 
        /// </summary>
        /// <param 