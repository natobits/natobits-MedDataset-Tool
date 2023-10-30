namespace InnerEye.CreateDataset.Data

///  ------------------------------------------------------------------------------------------
///  Copyright (c) Microsoft Corporation. All rights reserved.
///  Licensed under the MIT License (MIT). See LICENSE in the repo root for license information.
///  ------------------------------------------------------------------------------------------

open System
open InnerEye.CreateDataset.Volumes
open System.Diagnostics

/// Describes the size of a 3D volume.
[<CLIMutableAttribute>]
type Volume3DDimensions =
    {
        /// The size of the volume in the X dimension
        X: int
        /// The size of the volume in the Y dimension
        Y: int
        /// The size of the volume in the Z dimension
        Z: int
    }

    override this.ToString() = sprintf "%i x %i x %i" this.X this.Y this.Z

    /// Creates a Volume3DDimensions instance from the arguments.
    static member Create (dimX, dimY, dimZ) = { X = dimX; Y = dimY; Z = dimZ }

    /// Creates a VolumeDimensions instance that stores the size of the given Volume3D instance.
    static member Create (volume: Volume3D<_>) = 
        { X = volume.DimX; Y = volume.DimY; Z = volume.DimZ }

    /// Returns true if the volume dimensions in the present object are strictly smaller in each dimension
    /// than the volume dimensions given in the argument.
    member this.IsStrictlySmallerThan (other: Volume3DDimensions) = 
        this.X < other.X 
        && this.Y < other.Y
        && this.Z < other.Z
   

/// Stores a point as an (x, y, z) tuple, with an equality operation that uses relative difference.
[<CLIMutableAttribute>]
type Tuple3D = 
    {
        /// The component in X dimension.
        X: double
        /// The component in Y dimension.
        Y: double
        /// The component in Z dimension.
        Z: double
    }

    override this.ToString() = 
        sprintf "X = %g; Y = %g; Z = %g" this.X this.Y this.Z

    /// <summary>
    /// Gets whether the point stored in the present object and the point in the argument
    /// should be considered equal, when looking at componentwise relative difference.
    /// The function returns true if, along all 3 dimensions, the pairwise relative
    /// difference is below the given threshold value. If any of the dimensions has a
    /// mismatch, detailed information is printed to Trace.
    /// </summary>
    /// <param name="other">The other tuple to which the present object should be compared.</param>
    /// <param name="maximumRelativeDifference">The maximum allowed relative difference along a dimension.</param>
    /// <param name="loggingPrefix">If a dimension has differences over the allowed maximum,
    /// print details to TraceWarning, with this string printed before the dimension.</param>
    member this.HasSmallRelativeDifference (other: Tuple3D, maximumRelativeDifference, loggingPrefix) =
        let equal dimension (x: double) (y: double) = 
            let diff =
                match x with
                | 0.0 -> Math.Abs y
                | nonZeroX -> Math.Abs(1.0 - y / nonZeroX)
            let isEqual = diff <= maximumRelativeDifference
            if not isEqual then 
                sprintf "Relative difference in %s%s is %f, but only %f is allowed" loggingPrefix dimension diff maximumRelativeDifference 
                |> Trace.TraceWarning
            isEqual
        equal "X" this.X other.X && equal "Y" this.Y other.Y && equal "Z" this.Z other.Z

    /// <summary>
    /// Gets whether the point stored in the present object and the point in the argument
    /// should be considered equal, when looking at componentwise relative difference.
    /// The function returns true if, along all 3 dimensions, the pairwise relative
    /// difference is below the given threshold value. If any of the dimensions has a
    /// mismatch, detailed information is printed to Trace.
    /// </summary>
    /// <param name="other">The other tuple to which the present object should be compared.</param>
    /// <param name="maximumRelativeDifference">The maximum allowed relative difference along a dimension.</param>
    member this.HasSmallRelativeDifference (other: Tuple3D, maximumRelativeDifference) =
        this.HasSmallRelativeDifference(other, maximumRelativeDifference, String.Empty)

    /// <summary>
    /// Gets whether the point stored in the present object and the point in the argument
    /// should be considered equal, when looking at componentwise abolute difference.
    /// The function returns true if, along all 3 dimensions, the pairwise absolute
    /// difference is below the given threshold value. If any of the dimensions has a
    /// mismatch, detailed information is printed to Trace.
    /// </summary>
    /// <param name="other">The other tuple to which the present object should be compared.</param>
    /// <param name="maximumAbsoluteDifference">The maximum allowed absolute difference along a dimension.</param>
    /// <param name="loggingPrefix">If a dimension has differences over the allowed maximum,
    /// print details to TraceWarning, with this string printed before the dimension.</param>
    member this.HasSmallAbsoluteDifference (other: Tuple3D, maximumAbsoluteDifference, loggingPrefix) =
        let equal dimension (x: double) (y: double) = 
            let diff = Math.Abs(x - y) 
            let isEqual = diff <= maximumAbsoluteDifference
            if not isEqual then 
                sprintf "Absolute difference in %s%s is %f, but only %f is allowed" loggingPrefix dimension diff maximumAbsoluteDifference 
                |> Trace.TraceWarning
            isEqual
        equal "X" this.X other.X && equal "Y" this.Y other.Y && equal "Z" this.Z other.Z

    /// <s