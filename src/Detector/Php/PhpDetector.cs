﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using System;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.Oryx.Common.Extensions;
using Newtonsoft.Json;

namespace Microsoft.Oryx.Detector.Php
{
    /// <summary>
    /// An implementation of <see cref="IPlatformDetector"/> which detects PHP applications.
    /// </summary>
    public class PhpDetector : IPhpPlatformDetector
    {
        private readonly ILogger<PhpDetector> _logger;

        /// <summary>
        /// Creates an instance of <see cref="PhpDetector"/>.
        /// </summary>
        /// <param name="logger">The <see cref="ILogger{PhpDetector}"/>.</param>
        public PhpDetector(ILogger<PhpDetector> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc/>
        public PlatformDetectorResult Detect(DetectorContext context)
        {
            var sourceRepo = context.SourceRepo;

            string phpVersion = null;
            var hasComposerFile = sourceRepo.FileExists(PhpConstants.ComposerFileName);
            string appDirectory;
            if (hasComposerFile)
            {
                _logger.LogDebug($"File '{PhpConstants.ComposerFileName}' exists in source repo");
                phpVersion = GetVersion(context);
                appDirectory = Constants.RelativeRootDirectory;

            }
            else
            {
                _logger.LogDebug($"File '{PhpConstants.ComposerFileName}' does not exist in source repo");

                var files = sourceRepo.EnumerateFiles(PhpConstants.PhpFileNamePattern, searchSubDirectories: true);
                if (files != null && files.Any())
                {
                    _logger.LogInformation(
                        $"Found files with extension '{PhpConstants.PhpFileNamePattern}' " +
                        $"in the repo.");
                    appDirectory = RelativeDirectoryHelper.GetRelativeDirectoryToRoot(files.FirstOrDefault(), sourceRepo.RootPath);
                }
                else
                {
                    _logger.LogInformation(
                        $"Could not find any file with extension '{PhpConstants.PhpFileNamePattern}' " +
                        $"in the repo.");
                    return null;
                }
            }

            return new PlatformDetectorResult
            {
                Platform = PhpConstants.PlatformName,
                PlatformVersion = phpVersion,
                AppDirectory = appDirectory,
            };
        }

        private string GetVersion(DetectorContext context)
        {
            var version = GetVersionFromComposerFile(context);
            if (version != null)
            {
                return version;
            }
            _logger.LogDebug("Could not get version from the composer file. ");
            return null;
        }

        private string GetVersionFromComposerFile(DetectorContext context)
        {
            dynamic composerFile = null;
            try
            {
                var jsonContent = context.SourceRepo.ReadFile(PhpConstants.ComposerFileName);
                composerFile = JsonConvert.DeserializeObject(jsonContent);
            }
            catch (Exception ex)
            {
                // We just ignore errors, so we leave malformed composer.json files for Composer to handle,
                // not us. This prevents us from erroring out when Composer itself might be able to tolerate
                // some errors in the composer.json file.
                _logger.LogWarning(
                    ex,
                    $"Exception caught while trying to deserialize {PhpConstants.ComposerFileName.Hash()}");
            }

            return composerFile?.require?.php?.Value as string;
        }
    }
}
