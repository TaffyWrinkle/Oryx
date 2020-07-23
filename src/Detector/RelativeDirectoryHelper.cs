﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Oryx.Detector
{
    public class RelativeDirectoryHelper
    {
        public static string GetRelativeDirectoryToRoot(string filePath, string repoRoot)
        {
            var repoRootDir = new DirectoryInfo(repoRoot);
            var fileInfo = new FileInfo(filePath);
            var currDir = fileInfo.Directory;
            var parts = new List<string>();

            while (!string.Equals(currDir.FullName, repoRootDir.FullName, StringComparison.Ordinal))
            {
                parts.Insert(0, currDir.Name);
                currDir = currDir.Parent;
            }
            parts.Insert(0, ".");

            // Since we have different expressions of directory in different OS, normalize it to be like "./a/b", "./".
            return parts.Count == 1 ? Constants.RelativeRootDirectory : string.Join("/", parts.ToArray());
        }
    }
}
