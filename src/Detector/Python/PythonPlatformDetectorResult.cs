﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

namespace Microsoft.Oryx.Detector.Python
{
    /// <summary>
    /// Represents the model which contains Python specific detected metadata.
    /// </summary>
    public class PythonPlatformDetectorResult : PlatformDetectorResult
    {
        /// <summary>
        /// Gets or sets the value indicating if files of extension '.ipynb' exist at the root of the repo.
        /// </summary>
        public bool HasJupyterNotebookFiles { get; set; }

        /// <summary>
        /// Gets or sets the value indicating if 'environment.yml' or 'environment.yaml' files
        /// exist at the root of the repo.
        /// </summary>
        public bool HasCondaEnvironmentYmlFile { get; set; }

        /// <summary>
        /// Gets or sets the value indicating if 'requirements.txt' file exists at the root of the repo.
        /// </summary>
        public bool HasRequirementsTxtFile { get; set; }
    }
}
