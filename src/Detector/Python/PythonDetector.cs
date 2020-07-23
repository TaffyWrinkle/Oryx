﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Oryx.Common.Extensions;

namespace Microsoft.Oryx.Detector.Python
{
    /// <summary>
    /// An implementation of <see cref="IPlatformDetector"/> which detects Python applications.
    /// </summary>
    public class PythonDetector : IPythonPlatformDetector
    {
        private readonly ILogger<PythonDetector> _logger;
        private readonly DetectorOptions _options;

        /// <summary>
        /// Creates an instance of <see cref="PythonDetector"/>.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger{PythonDetector}"/>.</param>
        /// <param name="options">The <see cref="DetectorOptions"/>.</param>
        public PythonDetector(ILogger<PythonDetector> logger, IOptions<DetectorOptions> options)
        {
            _logger = logger;
            _options = options.Value;
        }

        /// <inheritdoc/>
        public PlatformDetectorResult Detect(DetectorContext context)
        {
            var sourceRepo = context.SourceRepo;
            var isPythonApp = IsPythonApp(sourceRepo, out string directory);

            // This detects if a runtime.txt file exists and if that is a python file
            var versionFromRuntimeFile = DetectPythonVersionFromRuntimeFile(context.SourceRepo);
            if (!isPythonApp && string.IsNullOrEmpty(versionFromRuntimeFile))
            {
                return null;
            }  

            return new PlatformDetectorResult
            {
                Platform = PythonConstants.PlatformName,
                PlatformVersion = versionFromRuntimeFile,
                Directory = directory,
            };
        }

        private bool IsPythonApp(ISourceRepo sourceRepo, out string directory)
        {
            directory = string.Empty;
            if (sourceRepo.FileExists(PythonConstants.RequirementsFileName))
            {
                directory = Constants.RelativeRootDirectory;
                _logger.LogInformation($"Found {PythonConstants.RequirementsFileName} at the root of the repo.");
                return true;
            }
            else
            {
                _logger.LogInformation(
                    $"Cound not find {PythonConstants.RequirementsFileName} at the root of the repo.");
            }

            var searchSubDirectories = !_options.DisableRecursiveLookUp;
            if (!searchSubDirectories)
            {
                _logger.LogDebug("Skipping search for files in sub-directories as it has been disabled.");
            }

            var files = sourceRepo.EnumerateFiles(PythonConstants.PythonFileNamePattern, searchSubDirectories);
            if (files != null && files.Any())
            {
                directory = RelativeDirectoryHelper.GetRelativeDirectoryToRoot(files.FirstOrDefault(), sourceRepo.RootPath);
                _logger.LogInformation(
                    $"Found files with extension '{PythonConstants.PythonFileNamePattern}' " +
                    $"in the repo.");
                return true;
            }
            else
            {
                _logger.LogInformation(
                    $"Could not find any file with extension '{PythonConstants.PythonFileNamePattern}' " +
                    $"in the repo.");
            }

            return false;
        }

        private string DetectPythonVersionFromRuntimeFile(ISourceRepo sourceRepo)
        {
            const string versionPrefix = "python-";

            // Most Python sites will have at least a .py file in the root, but
            // some may not. In that case, let them opt in with the runtime.txt
            // file, which is used to specify the version of Python.
            if (sourceRepo.FileExists(PythonConstants.RuntimeFileName))
            {
                try
                {
                    var content = sourceRepo.ReadFile(PythonConstants.RuntimeFileName);
                    var hasPythonVersion = content.StartsWith(versionPrefix, StringComparison.OrdinalIgnoreCase);
                    if (!hasPythonVersion)
                    {
                        _logger.LogDebug(
                            "Prefix {verPrefix} was not found in file {rtFileName}",
                            versionPrefix,
                            PythonConstants.RuntimeFileName.Hash());
                        return null;
                    }

                    var pythonVersion = content.Remove(0, versionPrefix.Length);
                    _logger.LogDebug(
                        "Found version {pyVer} in the {rtFileName} file",
                        pythonVersion,
                        PythonConstants.RuntimeFileName.Hash());
                    return pythonVersion;
                }
                catch (IOException ex)
                {
                    _logger.LogError(
                        ex,
                        "An error occurred while reading file {rtFileName}",
                        PythonConstants.RuntimeFileName);
                }
            }
            else
            {
                _logger.LogDebug(
                    "Could not find file '{rtFileName}' in source repo",
                    PythonConstants.RuntimeFileName);
            }

            return null;
        }
    }
}