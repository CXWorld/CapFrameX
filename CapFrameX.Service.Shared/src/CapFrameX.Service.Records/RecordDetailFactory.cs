using CapFrameX.Data.Session.Contracts;
using CapFrameX.Service.Contracts.Records;

namespace CapFrameX.Service.Records;

/// <summary>
/// Projects a capture onto what the analysis view shows above the chart.
/// </summary>
/// <remarks>
/// The chips are composed here rather than in the frontend: which facts are worth a pill, and what
/// counts as knowing one, is a decision about the capture format, and both platforms and any later
/// client should make it the same way.
/// </remarks>
public static class RecordDetailFactory
{
    /// <summary>What the capture recorded about the machine.</summary>
    /// <param name="session">The parsed capture.</param>
    public static RecordInfoDto Info(ISession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var info = session.Info;

        return new RecordInfoDto(
            GameName: Text(info?.GameName),
            ProcessName: Text(info?.ProcessName),
            Comment: Text(info?.Comment),
            Processor: Text(info?.Processor),
            Motherboard: Text(info?.Motherboard),
            SystemRam: Text(info?.SystemRam),
            Gpu: Text(info?.GPU),
            GpuCount: Text(info?.GPUCount),
            GpuCoreClock: Text(info?.GpuCoreClock),
            GpuMemoryClock: Text(info?.GpuMemoryClock),
            BaseDriverVersion: Text(info?.BaseDriverVersion),
            DriverPackage: Text(info?.DriverPackage),
            GpuDriverVersion: Text(info?.GPUDriverVersion),
            Os: Text(info?.OS),
            ApiInfo: Text(info?.ApiInfo),
            ResizableBar: Switch(info?.ResizableBar),
            WinGameMode: Switch(info?.WinGameMode),
            Hags: Switch(info?.HAGS),
            PresentationMode: Text(info?.PresentationMode),
            ResolutionInfo: Text(info?.ResolutionInfo),
            AppVersion: info?.AppVersion?.ToString(),
            DeviceName: Text(info?.DeviceName));
    }

    /// <summary>The capture's runs, in the order they were recorded.</summary>
    /// <param name="session">The parsed capture.</param>
    public static IReadOnlyList<RecordRunDto> Runs(ISession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var runs = session.Runs ?? [];
        var described = new List<RecordRunDto>(runs.Count);

        for (var index = 0; index < runs.Count; index++)
        {
            var data = runs[index]?.CaptureData;

            described.Add(new RecordRunDto(
                Index: index,
                DurationSeconds: Span(data?.TimeInSeconds),
                FrameCount: data?.MsBetweenPresents?.Length ?? 0,
                PresentMonRuntime: Text(runs[index]?.PresentMonRuntime),
                HasPcLatency: data?.PcLatency is { Length: > 0 },
                HasDisplayChange: data?.MsBetweenDisplayChange is { Length: > 0 },
                HasGpuActive: data?.GpuActive is { Length: > 0 }));
        }

        return described;
    }

    /// <summary>The pills above the analysis, in the order they are shown.</summary>
    /// <param name="session">The parsed capture.</param>
    public static IReadOnlyList<ChipDto> Chips(ISession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        var info = session.Info;
        var chips = new List<ChipDto>(8);

        Add(chips, ChipKeys.Processor, Text(info?.Processor));
        Add(chips, ChipKeys.Gpu, Text(info?.GPU));
        Add(chips, ChipKeys.Memory, Text(info?.SystemRam));
        Add(chips, ChipKeys.Api, Text(info?.ApiInfo));
        Add(chips, ChipKeys.Resolution, Text(info?.ResolutionInfo));
        Add(chips, ChipKeys.Presentation, Text(info?.PresentationMode));

        var driver = Text(info?.GPUDriverVersion);
        Add(chips, ChipKeys.Driver, driver is null ? null : $"Driver {driver}");

        // One pill for the switches rather than one each: they are only interesting together, and
        // a row of "Disabled" pills says nothing a reader wants.
        var features = new List<string>(3);

        if (Switch(info?.ResizableBar) is true)
        {
            features.Add("ReBAR");
        }

        if (Switch(info?.HAGS) is true)
        {
            features.Add("HAGS");
        }

        if (Switch(info?.WinGameMode) is true)
        {
            features.Add("Game Mode");
        }

        Add(chips, ChipKeys.Features, features.Count == 0 ? null : string.Join(" · ", features));

        return chips;
    }

    private static void Add(List<ChipDto> chips, string key, string? label)
    {
        if (label is not null)
        {
            chips.Add(new ChipDto(key, label));
        }
    }

    /// <summary>
    /// Reads one of the platform switches.
    /// </summary>
    /// <remarks>
    /// CapFrameX writes "Enabled" or "Disabled", and an empty string where it could not find out -
    /// which is a third state, not a false one, so it comes back as no answer.
    /// </remarks>
    private static bool? Switch(string? value) => Text(value) switch
    {
        null => null,
        var text when text.Equals("Enabled", StringComparison.OrdinalIgnoreCase) => true,
        var text when text.Equals("Disabled", StringComparison.OrdinalIgnoreCase) => false,
        _ => null,
    };

    /// <summary>The time the frames cover, which is not their count times a frame time.</summary>
    private static double Span(double[]? timeInSeconds) =>
        timeInSeconds is { Length: > 1 } ? timeInSeconds[^1] - timeInSeconds[0] : 0d;

    private static string? Text(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
