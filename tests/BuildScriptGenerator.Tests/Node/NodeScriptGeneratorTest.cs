// --------------------------------------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// --------------------------------------------------------------------------------------------

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Oryx.BuildScriptGenerator.Node;
using Oryx.Tests.Common;
using Xunit;

namespace Microsoft.Oryx.BuildScriptGenerator.Tests
{
    /// <summary>
    /// Component tests for NodeJs support.
    /// </summary>
    public class NodeScriptGeneratorTest
    {
        private const string PackageJsonWithBuildScript = @"{
          ""name"": ""mynodeapp"",
          ""version"": ""1.0.0"",
          ""description"": ""test app"",
          ""main"": ""server.js"",
          ""scripts"": {
            ""build"": ""tsc"",
            ""build:azure"": ""tsc"",
          },
          ""author"": ""Dev"",
          ""license"": ""ISC"",
          ""dependencies"": {
            ""@types/node"": ""7.0.22"",
            ""express"": ""4.15.3"",
          },
          ""devDependencies"": {
            ""@types/mocha"": ""2.2.41"",
            ""@types/node"": ""7.0.22"",
            ""tsc"": ""1.20150623.0"",
            ""typescript"": ""2.3.3"",
            ""typescript-eslint-parser"": ""3.0.0""
          }
        }";

        private const string PackageJsonWithNoNpmVersion = @"{
          ""name"": ""mynodeapp"",
          ""version"": ""1.0.0"",
          ""description"": ""test app"",
          ""main"": ""server.js"",
          ""scripts"": {
            ""test"": ""echo \""Error: no test specified\"" && exit 1"",
            ""start"": ""node server.js""
          },
          ""author"": ""Dev"",
          ""license"": ""ISC"",
          ""engines"" : { ""node"" : ""6.11.0"" }
        }";

        private const string PackageJsonWithNpmVersion = @"{
          ""name"": ""mynodeapp"",
          ""version"": ""1.0.0"",
          ""description"": ""test app"",
          ""main"": ""server.js"",
          ""scripts"": {
            ""test"": ""echo \""Error: no test specified\"" && exit 1"",
            ""start"": ""node server.js""
          },
          ""author"": ""Dev"",
          ""license"": ""ISC"",
          ""engines"" : { ""npm"" : ""5.4.2"" },
          ""dependencies"": { ""foo"" : ""1.0.0 - 2.9999.9999"", ""bar"" : "">=1.0.2 <2.1.2"" }
        }";

        private const string PackageJsonWithUnsupportedNpmVersion = @"{
          ""name"": ""mynodeapp"",
          ""version"": ""1.0.0"",
          ""description"": ""test app"",
          ""main"": ""server.js"",
          ""scripts"": {
            ""test"": ""echo \""Error: no test specified\"" && exit 1"",
            ""start"": ""node server.js""
          },
          ""author"": ""Dev"",
          ""license"": ""ISC"",
          ""engines"" : { ""node"" : ""100.100.100"" }
        }";

        private const string MalformedPackageJson = @"{
          ""name"": ""mynodeapp"",
          ""version"": ""1.0.0"",
          ""description"": ""test app"",
          ""main"": ""server.js"",
          ""scripts"": {
            ""test"": ""echo ,
            ""start"": ""node server.js""
          },
          ""author"": ""Dev"",
          ""license"": ""ISC""
        }";

        private const string NpmInstallCommand = "npm install";
        private const string YarnInstallCommand = "yarn install";

        [Fact]
        public void TryGenerateBashScript_ReturnsFalse_WhenPackageJsonHas_UnsupportedNpmVersion()
        {
            // Arrange
            var scriptGenerator = GetScriptGenerator(defaultNpmVersion: "5.4.2");
            var repo = new CachedSourceRepo();
            repo.AddFile(PackageJsonWithUnsupportedNpmVersion, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            context.LanguageVersion = "8.2.1";

            // Act
            var canGenerateScript = scriptGenerator.TryGenerateBashScript(context, out var generatedScriptContent);
        }

        [Fact]
        public void GeneratedScript_HasNpmVersion_SpecifiedInPackageJson()
        {
            // Arrange
            var scriptGenerator = GetScriptGenerator(defaultNpmVersion: "6.0.0");
            var repo = new CachedSourceRepo();
            repo.AddFile(PackageJsonWithNpmVersion, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            context.LanguageVersion = "8.2.1";
            var expected = new NodeBashBuildScript(
                preBuildScriptPath: null,
                packageInstallCommand: NpmInstallCommand,
                runBuildCommand: null,
                runBuildAzureCommand: null,
                benvArgs: "node=8.2.1 npm=5.4.2",
                postBuildScriptPath: null);

            // Act
            var canGenerateScript = scriptGenerator.TryGenerateBashScript(context, out var generatedScriptContent);

            // Assert
            Assert.True(canGenerateScript);
            Assert.Equal(expected.TransformText(), generatedScriptContent);
        }

        [Fact]
        public void GeneratedScript_HasDefaultNpmVersion_IfPackageJsonDoesNotHaveOne()
        {
            // Arrange
            var scriptGenerator = GetScriptGenerator(defaultNpmVersion: "6.0.0");
            var repo = new CachedSourceRepo();
            repo.AddFile(PackageJsonWithNoNpmVersion, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            context.LanguageVersion = "8.2.1";
            var expected = new NodeBashBuildScript(
                preBuildScriptPath: null,
                benvArgs: "node=8.2.1 npm=6.0.0",
                packageInstallCommand: NpmInstallCommand,
                runBuildCommand: null,
                runBuildAzureCommand: null,
                postBuildScriptPath: null);

            // Act
            var canGenerateScript = scriptGenerator.TryGenerateBashScript(context, out var generatedScriptContent);

            // Assert
            Assert.True(canGenerateScript);
            Assert.Equal(expected.TransformText(), generatedScriptContent);
        }

        [Fact]
        public void GeneratesScript_WithDefaultNpmVersion_ForMalformedPackageJson()
        {
            // Arrange
            var scriptGenerator = GetScriptGenerator(defaultNpmVersion: "5.4.2");
            var repo = new CachedSourceRepo();
            repo.AddFile(MalformedPackageJson, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            context.LanguageVersion = "8.2.1";
            var expected = new NodeBashBuildScript(
                preBuildScriptPath: null,
                benvArgs: "node=8.2.1 npm=5.4.2",
                packageInstallCommand: NpmInstallCommand,
                runBuildCommand: null,
                runBuildAzureCommand: null,
                postBuildScriptPath: null);

            // Act
            var canGenerateScript = scriptGenerator.TryGenerateBashScript(context, out var generatedScriptContent);

            // Assert
            Assert.True(canGenerateScript);
            Assert.Equal(expected.TransformText(), generatedScriptContent);
        }

        [Fact]
        public void GeneratedScript_UsesYarnInstall_IfYarnLockFileIsPresent()
        {
            // Arrange
            var scriptGenerator = GetScriptGenerator(defaultNpmVersion: "6.0.0");
            var repo = new CachedSourceRepo();
            repo.AddFile(PackageJsonWithNoNpmVersion, NodeConstants.PackageJsonFileName);
            repo.AddFile("Yarn lock file content here", NodeConstants.YarnLockFileName);
            var context = CreateScriptGeneratorContext(repo);
            context.LanguageVersion = "8.2.1";
            var expected = new NodeBashBuildScript(
                preBuildScriptPath: null,
                benvArgs: "node=8.2.1",
                packageInstallCommand: YarnInstallCommand,
                runBuildCommand: null,
                runBuildAzureCommand: null,
                postBuildScriptPath: null);

            // Act
            var canGenerateScript = scriptGenerator.TryGenerateBashScript(context, out var generatedScriptContent);

            // Assert
            Assert.True(canGenerateScript);
            Assert.Equal(expected.TransformText(), generatedScriptContent);
        }

        [Fact]
        public void GeneratedScript_UsesYarnInstallAndRunsNpmBuild_IfYarnLockFileIsPresent_AndBuildNodeIsPresentUnderScripts()
        {
            // Arrange
            var scriptGenerator = GetScriptGenerator(defaultNpmVersion: "6.0.0");
            var repo = new CachedSourceRepo();
            repo.AddFile(PackageJsonWithBuildScript, NodeConstants.PackageJsonFileName);
            repo.AddFile("Yarn lock file content here", NodeConstants.YarnLockFileName);
            var context = CreateScriptGeneratorContext(repo);
            context.LanguageVersion = "8.2.1";
            var expected = new NodeBashBuildScript(
                 preBuildScriptPath: null,
                benvArgs: "node=8.2.1",
                packageInstallCommand: YarnInstallCommand,
                runBuildCommand: "yarn run build",
                runBuildAzureCommand: "yarn run build:azure",
                postBuildScriptPath: null);

            // Act
            var canGenerateScript = scriptGenerator.TryGenerateBashScript(context, out var generatedScriptContent);

            // Assert
            Assert.True(canGenerateScript);
            string extectedText = expected.TransformText();
            Assert.Equal(extectedText, generatedScriptContent);
        }

        [Fact]
        public void GeneratedScript_UsesNpmInstall_IfPackageLockJsonFileIsPresent()
        {
            // Arrange
            var scriptGenerator = GetScriptGenerator(defaultNpmVersion: "6.0.0");
            var repo = new CachedSourceRepo();
            repo.AddFile(PackageJsonWithNoNpmVersion, NodeConstants.PackageJsonFileName);
            repo.AddFile("Package lock json file content here", NodeConstants.PackageLockJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            context.LanguageVersion = "8.2.1";
            var expected = new NodeBashBuildScript(
                preBuildScriptPath: null,
                benvArgs: "node=8.2.1 npm=6.0.0",
                packageInstallCommand: NpmInstallCommand,
                runBuildCommand: null,
                runBuildAzureCommand: null,
                postBuildScriptPath: null);

            // Act
            var canGenerateScript = scriptGenerator.TryGenerateBashScript(context, out var generatedScriptContent);

            // Assert
            Assert.True(canGenerateScript);
            Assert.Equal(expected.TransformText(), generatedScriptContent);
        }

        [Fact]
        public void GeneratedScript_UsesNpmRunBuild_IfBuildNodeIsPresentUnderScripts()
        {
            // Arrange
            var scriptGenerator = GetScriptGenerator(defaultNpmVersion: "6.0.0");
            var repo = new CachedSourceRepo();
            repo.AddFile(PackageJsonWithBuildScript, NodeConstants.PackageJsonFileName);
            var context = CreateScriptGeneratorContext(repo);
            context.LanguageVersion = "8.2.1";
            var expected = new NodeBashBuildScript(
                preBuildScriptPath: null,
                benvArgs: "node=8.2.1 npm=6.0.0",
                packageInstallCommand: NpmInstallCommand,
                runBuildCommand: "npm run build",
                runBuildAzureCommand: "npm run build:azure",
                postBuildScriptPath: null);

            // Act
            var canGenerateScript = scriptGenerator.TryGenerateBashScript(context, out var generatedScriptContent);

            // Assert
            Assert.True(canGenerateScript);
            Assert.Equal(expected.TransformText(), generatedScriptContent);
        }

        private ILanguageScriptGenerator GetScriptGenerator(string defaultNodeVersion = null, string defaultNpmVersion = null)
        {
            var environment = new TestEnvironment();
            environment.Variables[NodeScriptGeneratorOptionsSetup.NodeJsDefaultVersion] = defaultNodeVersion;
            environment.Variables[NodeScriptGeneratorOptionsSetup.NpmDefaultVersion] = defaultNpmVersion;

            var nodeVersionProvider = new TestNodeVersionProvider(
                supportedNodeVersions: new[] { "6.11.0", "8.2.1" },
                supportedNpmVersions: new[] { "5.4.2", "6.0.0" });

            var nodeScriptGeneratorOptions = Options.Create(new NodeScriptGeneratorOptions());
            var optionsSetup = new NodeScriptGeneratorOptionsSetup(environment);
            optionsSetup.Configure(nodeScriptGeneratorOptions.Value);

            return new NodeScriptGenerator(
                nodeScriptGeneratorOptions,
                nodeVersionProvider,
                new TestEnvironmentSettingsProvider(),
                NullLogger<NodeScriptGenerator>.Instance);
        }

        private static ScriptGeneratorContext CreateScriptGeneratorContext(
            ISourceRepo sourceRepo,
            string languageName = null,
            string languageVersion = null)
        {
            return new ScriptGeneratorContext
            {
                Language = languageName,
                LanguageVersion = languageVersion,
                SourceRepo = sourceRepo
            };
        }
    }
}