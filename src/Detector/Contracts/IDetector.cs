﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System.Collections.Generic;

namespace Microsoft.Oryx.Detector
{
    /// <summary>
    /// An abstraction over the detection of all platforms in a given repository.
    /// </summary>
    public interface IDetector
    {
        /// <summary>
        /// Returns all platforms detected for the given repository context.
        /// </summary>
        /// <param name="context">The <see cref="DetectorContext"/>.</param>
        /// <returns>A list of platform detection results containing useful information, such as platform name, platform version, etc.</returns>
        IEnumerable<PlatformDetectorResult> GetAllDetectedPlatforms(DetectorContext context);
    }
}