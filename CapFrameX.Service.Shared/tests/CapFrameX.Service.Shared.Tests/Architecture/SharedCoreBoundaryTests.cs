using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using CapFrameX.Service.Application.Demand;
using CapFrameX.Service.Contracts.Frames;
using CapFrameX.Service.Core.Platform;

namespace CapFrameX.Service.Shared.Tests.Architecture;

/// <summary>
/// The two services are only interchangeable as long as the shared core stays platform-neutral.
/// That rule is invisible in a diff - a single <c>OperatingSystem.IsWindows()</c> or one reference
/// to a platform assembly compiles fine and is caught by nobody - so it is asserted here.
/// </summary>
public sealed class SharedCoreBoundaryTests
{
    private static readonly Assembly[] SharedAssemblies =
    [
        typeof(FrameSample).Assembly,
        typeof(IFrameSource).Assembly,
        typeof(DemandRegistry).Assembly,
    ];

    public static TheoryData<string> Shared =>
        new([.. SharedAssemblies.Select(a => a.GetName().Name!)]);

    private static Assembly Resolve(string name) =>
        SharedAssemblies.Single(a => a.GetName().Name == name);

    [Theory]
    [MemberData(nameof(Shared))]
    public void Shared_assembly_does_not_reference_a_platform_assembly(string assemblyName)
    {
        var referenced = Resolve(assemblyName)
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => n.StartsWith("CapFrameX.Service.Windows", StringComparison.Ordinal)
                     || n.StartsWith("CapFrameX.Service.Linux", StringComparison.Ordinal)
                     || n.StartsWith("CapFrameX.Service.Monitoring", StringComparison.Ordinal))
            .ToArray();

        Assert.True(
            referenced.Length == 0,
            $"{assemblyName} references platform assemblies: {string.Join(", ", referenced)}. "
            + "Platform work belongs behind a port in CapFrameX.Service.Core.Platform.");
    }

    [Theory]
    [MemberData(nameof(Shared))]
    public void Shared_assembly_does_not_branch_on_the_operating_system(string assemblyName)
    {
        // Checking referenced assembly names is not enough: RuntimeInformation and OperatingSystem
        // are type-forwarded to System.Runtime, so a using of either leaves the reference list
        // unchanged. The type references in the metadata are what actually name them.
        var forbidden = new[]
        {
            "System.Runtime.InteropServices.RuntimeInformation",
            "System.OperatingSystem",
        };

        var used = TypeReferences(Resolve(assemblyName))
            .Where(t => forbidden.Contains(t, StringComparer.Ordinal))
            .Distinct()
            .ToArray();

        Assert.True(
            used.Length == 0,
            $"{assemblyName} uses {string.Join(", ", used)}. Composition is a compile-time property "
            + "of the host; the shared core must not ask which operating system it is on.");
    }

    /// <summary>Every type the assembly references from outside itself, as namespace-qualified names.</summary>
    private static IEnumerable<string> TypeReferences(Assembly assembly)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var peReader = new PEReader(stream);
        var metadata = peReader.GetMetadataReader();

        foreach (var handle in metadata.TypeReferences)
        {
            var reference = metadata.GetTypeReference(handle);
            var ns = metadata.GetString(reference.Namespace);
            var name = metadata.GetString(reference.Name);
            yield return string.IsNullOrEmpty(ns) ? name : ns + "." + name;
        }
    }

    [Theory]
    [MemberData(nameof(Shared))]
    public void Shared_assembly_declares_no_platform_specific_api(string assemblyName)
    {
        var attribute = Resolve(assemblyName)
            .GetCustomAttributes(inherit: false)
            .Select(a => a.GetType().Name)
            .FirstOrDefault(n => n == "SupportedOSPlatformAttribute");

        Assert.Null(attribute);
    }

    [Fact]
    public void Every_platform_port_lives_in_the_one_namespace_hosts_implement()
    {
        // A port outside this namespace is a port the composition roots will not think to wire.
        var ports = typeof(IFrameSource).Assembly
            .GetExportedTypes()
            .Where(t => t.IsInterface && t.Name.StartsWith('I'))
            .Where(t => t.Name is "IFrameSource" or "IFrameSubscription" or "ITelemetrySource"
                        or "IHotkeyBackend" or "IOverlayBackend" or "IAppPaths" or "IPrivilegeInfo")
            .ToArray();

        Assert.Equal(7, ports.Length);
        Assert.All(ports, p => Assert.Equal("CapFrameX.Service.Core.Platform", p.Namespace));
    }
}
