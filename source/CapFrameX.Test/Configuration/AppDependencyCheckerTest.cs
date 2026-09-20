using System;
using System.IO;
using System.Reflection;
using System.Runtime.Versioning;
using CapFrameX.Configuration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CapFrameX.Test.Configuration
{
    [TestClass]
    public class AppDependencyCheckerTest
    {
        private string _dotNetBasePath;

        [TestInitialize]
        public void Initialize()
        {
            _dotNetBasePath = Path.Combine(
                Path.GetTempPath(),
                "CapFrameX.DependencyCheckerTests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dotNetBasePath);
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (Directory.Exists(_dotNetBasePath))
            {
                Directory.Delete(_dotNetBasePath, true);
            }
        }

        [DataTestMethod]
        [DataRow("10.0.0")]
        [DataRow("10.0.11")]
        [DataRow("10.1.0")]
        public void CheckMissingDependencies_CompatibleDesktopRuntime_IsValid(string version)
        {
            AddDesktopRuntime(version);

            var report = AppDependencyChecker.CheckMissingDependencies(
                _dotNetBasePath,
                isVCRedistx64Installed: true);

            Assert.IsTrue(report.Valid);
            Assert.IsNull(report.MissingDotNetFrameworkVersion);
            Assert.IsNull(report.MissingVCRedistVersions);
        }

        [DataTestMethod]
        [DataRow("9.0.19")]
        [DataRow("11.0.0")]
        [DataRow("10.0.0-preview.7")]
        [DataRow("not-a-version")]
        public void CheckMissingDependencies_IncompatibleDesktopRuntime_ReportsDotNet10Missing(string version)
        {
            AddDesktopRuntime(version);

            var report = AppDependencyChecker.CheckMissingDependencies(
                _dotNetBasePath,
                isVCRedistx64Installed: true);

            Assert.IsFalse(report.Valid);
            Assert.AreEqual("10.0 (x64)", report.MissingDotNetFrameworkVersion);
            Assert.IsNull(report.MissingVCRedistVersions);
        }

        [TestMethod]
        public void CheckMissingDependencies_NoDotNetInstallation_ReportsDotNet10Missing()
        {
            Directory.Delete(_dotNetBasePath, true);

            var report = AppDependencyChecker.CheckMissingDependencies(
                _dotNetBasePath,
                isVCRedistx64Installed: true);

            Assert.IsFalse(report.Valid);
            Assert.AreEqual("10.0 (x64)", report.MissingDotNetFrameworkVersion);
        }

        [TestMethod]
        public void CheckMissingDependencies_EmptyDesktopRuntimeDirectory_ReportsDotNet10Missing()
        {
            AddComponent("shared", "Microsoft.NETCore.App", "10.0.11");
            Directory.CreateDirectory(Path.Combine(
                _dotNetBasePath,
                "shared",
                "Microsoft.WindowsDesktop.App",
                "10.0.11"));

            var report = AppDependencyChecker.CheckMissingDependencies(
                _dotNetBasePath,
                isVCRedistx64Installed: true);

            Assert.IsFalse(report.Valid);
            Assert.AreEqual("10.0 (x64)", report.MissingDotNetFrameworkVersion);
        }

        [TestMethod]
        public void CheckMissingDependencies_DesktopComponentWithoutCoreRuntime_ReportsDotNet10Missing()
        {
            AddComponent("shared", "Microsoft.WindowsDesktop.App", "10.0.11");

            var report = AppDependencyChecker.CheckMissingDependencies(
                _dotNetBasePath,
                isVCRedistx64Installed: true);

            Assert.IsFalse(report.Valid);
            Assert.AreEqual("10.0 (x64)", report.MissingDotNetFrameworkVersion);
        }

        [DataTestMethod]
        [DataRow("shared", "Microsoft.NETCore.App")]
        [DataRow("sdk", "10.0.400")]
        public void CheckMissingDependencies_NonDesktopComponentOnly_ReportsDotNet10Missing(
            string firstPathSegment,
            string secondPathSegment)
        {
            if (firstPathSegment == "sdk")
            {
                AddSdk(secondPathSegment);
            }
            else
            {
                AddComponent(firstPathSegment, secondPathSegment, "10.0.11");
            }

            var report = AppDependencyChecker.CheckMissingDependencies(
                _dotNetBasePath,
                isVCRedistx64Installed: true);

            Assert.IsFalse(report.Valid);
            Assert.AreEqual("10.0 (x64)", report.MissingDotNetFrameworkVersion);
        }

        [TestMethod]
        public void CheckMissingDependencies_MissingVCRedist_ReportsOnlyVCRedist()
        {
            AddDesktopRuntime("10.0.11");

            var report = AppDependencyChecker.CheckMissingDependencies(
                _dotNetBasePath,
                isVCRedistx64Installed: false);

            Assert.IsFalse(report.Valid);
            Assert.IsNull(report.MissingDotNetFrameworkVersion);
            CollectionAssert.AreEqual(
                new[] { "2015-2022 (x64)" },
                report.MissingVCRedistVersions);
        }

        [TestMethod]
        public void GetInstalledRequiredDotNetComponents_ReturnsAllMatchingComponents()
        {
            AddComponent("shared", "Microsoft.NETCore.App", "10.0.11");
            AddComponent("shared", "Microsoft.WindowsDesktop.App", "10.0.11");
            AddSdk("10.0.400");

            var components = AppDependencyChecker.GetInstalledRequiredDotNetComponents(_dotNetBasePath);

            Assert.AreEqual(
                DotNetComponents.Runtime | DotNetComponents.DesktopRuntime | DotNetComponents.Sdk,
                components);
        }

        [TestMethod]
        public void RequiredDotNetMajorVersion_MatchesConfigurationTargetFramework()
        {
            var targetFramework = typeof(AppDependencyChecker).Assembly
                .GetCustomAttribute<TargetFrameworkAttribute>();

            Assert.IsNotNull(targetFramework);
            var frameworkName = new FrameworkName(targetFramework.FrameworkName);
            Assert.AreEqual(AppDependencyChecker.MajorDotNetVersionRequired, frameworkName.Version.Major);
        }

        private void AddDesktopRuntime(string version)
        {
            AddComponent("shared", "Microsoft.NETCore.App", version);
            AddComponent("shared", "Microsoft.WindowsDesktop.App", version);
        }

        private void AddComponent(string firstPathSegment, string componentName, string version)
        {
            var versionPath = Path.Combine(
                _dotNetBasePath,
                firstPathSegment,
                componentName,
                version);
            Directory.CreateDirectory(versionPath);

            string markerFileName = componentName == "Microsoft.WindowsDesktop.App"
                ? "PresentationFramework.dll"
                : "System.Private.CoreLib.dll";
            File.WriteAllText(Path.Combine(versionPath, markerFileName), string.Empty);
        }

        private void AddSdk(string version)
        {
            var versionPath = Path.Combine(_dotNetBasePath, "sdk", version);
            Directory.CreateDirectory(versionPath);
            File.WriteAllText(Path.Combine(versionPath, "dotnet.dll"), string.Empty);
        }
    }
}
