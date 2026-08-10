using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("CapFrameX.Test")]
[assembly: AssemblyDescription("")]
[assembly: AssemblyConfiguration("")]
[assembly: AssemblyCompany("")]
[assembly: AssemblyProduct("CapFrameX.Test")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: AssemblyTrademark("")]
[assembly: AssemblyCulture("")]

[assembly: ComVisible(false)]

[assembly: Guid("55776cce-1b05-4cca-aca9-20c0d84ccdca")]

// [assembly: AssemblyVersion("1.0.*")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

// net9.0-windows implies this attribute, but the SDK only emits it when GenerateAssemblyInfo is
// on, and this project keeps that off for the hand-written attributes above. Without it CA1416
// treats every call site as platform neutral and flags each Windows-only API. 7.0 is the platform
// minimum that net9.0-windows targets by default.
[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows7.0")]
