﻿// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license.
// --------------------------------------------------------------------------------------------

using Xunit;
using Xunit.Abstractions;
using Microsoft.Oryx.BuildScriptGenerator.Node;
using Microsoft.Oryx.BuildScriptGenerator.Php;
using Microsoft.Oryx.Common;
using Microsoft.Oryx.Tests.Common;
using Microsoft.Oryx.BuildScriptGenerator.DotNetCore;
using System;

namespace Microsoft.Oryx.BuildImage.Tests
{
    public class OryxCommandTest : SampleAppsTestBase
    {
        public OryxCommandTest(ITestOutputHelper output) : base(output) { }

        [Fact]
        public void BuildImage_Build_UsesCwd_WhenNoSourceDirGiven()
        {
            // Act
            var result = _dockerCli.Run(new DockerRunArguments
            {
                ImageId = Settings.BuildImageName,
                CommandToExecuteOnRun = "oryx",
                CommandArguments = new[] { "build" },
                WorkingDirectory = "/tmp"
            });

            // Assert
            RunAsserts(
                () =>
                {
                    Assert.Contains("Error: Could not detect", result.StdErr);
                    Assert.DoesNotContain("does not exist", result.StdErr);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void BuildImage_CanExec_WithNoUsableToolsDetected()
        {
            // Arrange
            var appPath = "/tmp";
            var cmd = "node --version";
            var script = new ShellScriptBuilder()
                .AddCommand($"oryx exec --debug --src {appPath} '{cmd}'") // '--debug' prints the resulting script
                .ToString();

            // Act
            var result = _dockerCli.Run(Settings.BuildImageName, "/bin/bash", "-c", script);

            // Assert
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    // Actual output from `node --version` starts with a 'v'
                    Assert.Contains($"v{NodeConstants.NodeLtsVersion}", result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void BuildImage_CanExec_SingleCommand()
        {
            // Arrange
            var appPath = "/tmp";
            var cmd = "node --version";
            var expectedBashPath = FilePaths.Bash;
            var script = new ShellScriptBuilder()
                .CreateFile($"{appPath}/{NodeConstants.PackageJsonFileName}", "{}")
                .AddCommand($"oryx exec --debug --src {appPath} '{cmd}'") // '--debug' prints the resulting script
                .ToString();

            // Act
            var result = _dockerCli.Run(Settings.BuildImageName, "/bin/bash", "-c", script);

            // Assert
            RunAsserts(
                () =>
                {
                    Assert.Contains("#!" + expectedBashPath, result.StdOut);
                    Assert.Contains($"node={NodeConstants.NodeLtsVersion}", result.StdOut);
                    Assert.True(result.IsSuccess);
                    // Actual output from `node --version` starts with a 'v'
                    Assert.Contains($"v{NodeConstants.NodeLtsVersion}", result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void BuildImage_CanExec_CommandInSourceDir()
        {
            // Arrange
            var appPath = "/tmp";
            var repoScriptPath = "userScript.sh";
            var absScriptPath = $"{appPath}/{repoScriptPath}";

            var script = new ShellScriptBuilder()
                .CreateFile($"{appPath}/{NodeConstants.PackageJsonFileName}", "{}")
                .CreateFile(absScriptPath, "node --version")
                .SetExecutePermissionOnFile(absScriptPath)
                .AddCommand($"oryx exec --debug --src {appPath} ./{repoScriptPath}")
                .ToString();

            // Act
            var result = _dockerCli.Run(Settings.BuildImageName, "/bin/bash", "-c", script);

            // Assert
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    // Actual output from `node --version` starts with a 'v'
                    Assert.Contains($"v{NodeConstants.NodeLtsVersion}", result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void BuildImage_CanExec_MultipleCommands_WithOlderToolVersions()
        {
            // Arrange
            var appPath = "/tmp";
            var cmd = "node --version && php --version";

            var expectedNodeVersion = NodeVersions.Node8Version;
            var expectedPhpVersion  = PhpVersions.Php72Version;

            var script = new ShellScriptBuilder()
                .CreateFile($"{appPath}/{NodeConstants.PackageJsonFileName}",
                    "'{\"engines\": {\"node\": \"" + expectedNodeVersion + "\"}}'")
                .CreateFile($"{appPath}/{PhpConstants.ComposerFileName}",
                    "'{\"require\": {\"php\": \"" + expectedPhpVersion + "\"}}'")
                .AddCommand($"oryx exec --debug --src {appPath} '{cmd}'") // '--debug' prints the resulting script
                .ToString();

            // Act
            var result = _dockerCli.Run(Settings.BuildImageName, "/bin/bash", "-c", script);

            // Assert
            RunAsserts(
                () =>
                {
                    Assert.Contains($"node={expectedNodeVersion} php={expectedPhpVersion}", result.StdOut);
                    Assert.True(result.IsSuccess);
                    // Actual output from `node --version` starts with a 'v'
                    Assert.Contains($"v{expectedNodeVersion}", result.StdOut);
                    // Actual output from `php --version`
                    Assert.Contains($"PHP {expectedPhpVersion}", result.StdOut);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void BuildImage_Exec_PropagatesFailures()
        {
            // Arrange
            var appPath = "/tmp";
            var expectedExitCode = 123;
            var cmd = $"exit {expectedExitCode}";

            var script = new ShellScriptBuilder()
                .CreateFile($"{appPath}/{NodeConstants.PackageJsonFileName}", "{}")
                .AddCommand($"oryx exec --debug --src {appPath} '{cmd}'")
                .ToString();

            // Act
            var result = _dockerCli.Run(Settings.BuildImageName, "/bin/bash", "-c", script);

            // Assert
            RunAsserts(
                () =>
                {
                    Assert.False(result.IsSuccess);
                    Assert.Equal(expectedExitCode, result.ExitCode);
                },
                result.GetDebugInfo());
        }

        [Fact]
        public void CliImage_Dockerfile_SucceedsWithBasicNodeApp()
        {
            // Arrange
            var appPath = "/tmp";
            var platformName = "nodejs";
            var runtimeName = ConvertToRuntimeName(platformName);
            var platformVersion = "10.17";
            var repositoryName = "build";
            var tagName = "slim";
            var script = new ShellScriptBuilder()
                .CreateFile($"{appPath}/{NodeConstants.PackageJsonFileName}", "{}")
                .AddCommand($"oryx dockerfile {appPath} --platform {platformName} --platform-version {platformVersion}")
                .ToString();

            // Act
            var result = _dockerCli.Run(_imageHelper.GetTestCliImage(), "/bin/bash", "-c", script);

            // Assert
            RunAsserts(
                () =>
                {
                    Assert.True(result.IsSuccess);
                    Assert.Contains($"{runtimeName}:{platformVersion}", result.StdOut);
                    Assert.Contains($"{repositoryName}:{tagName}", result.StdOut);
                },
                result.GetDebugInfo());
        }

        private string ConvertToRuntimeName(string platformName)
        {
            if (string.Equals(platformName, DotNetCoreConstants.LanguageName, StringComparison.OrdinalIgnoreCase))
            {
                platformName = "dotnetcore";
            }

            if (string.Equals(platformName, NodeConstants.NodeJsName, StringComparison.OrdinalIgnoreCase))
            {
                platformName = "node";
            }

            return platformName;
        }

    }
}
