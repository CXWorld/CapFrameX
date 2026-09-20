using CapFrameX.Data.Session.Classes;
using CapFrameX.Data.Session.Contracts;
using Newtonsoft.Json;
using LegacyRun = CapFrameX.Data.Session.Classes.SessionRun;
using LegacySession = CapFrameX.Data.Session.Classes.Session;
using StoredRun = CapFrameX.Service.Data.Models.SessionRun;
using StoredSession = CapFrameX.Service.Data.Models.Session;

namespace CapFrameX.Service.Application.Records;

/// <summary>
/// Rebuilds a capture from what the database holds.
/// </summary>
/// <remarks>
/// An imported record has no file behind it, so the analysis gets its capture from here instead.
/// The result is the same model a file produces, which is why the statistics, the detail view and
/// the series need to know nothing about where a record came from.
/// </remarks>
public static class StoredRecordFactory
{
    /// <summary>Whether a record carries its capture rather than pointing at one.</summary>
    /// <param name="runs">The record's runs.</param>
    public static bool HasCapture(IEnumerable<StoredRun> runs)
    {
        ArgumentNullException.ThrowIfNull(runs);

        return runs.Any(run => !string.IsNullOrEmpty(run.CaptureDataJson));
    }

    /// <summary>Builds the capture a record holds.</summary>
    /// <param name="record">The indexed row.</param>
    /// <param name="runs">Its runs, in the order they were recorded.</param>
    public static ISession Rebuild(StoredSession record, IReadOnlyList<StoredRun> runs)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(runs);

        var session = new LegacySession
        {
            Hash = record.Hash,
            Info = Info(record),
            Runs = new List<ISessionRun>(runs.Count),
        };

        foreach (var run in runs)
        {
            session.Runs.Add(new LegacyRun
            {
                Hash = run.Hash,
                PresentMonRuntime = run.PresentMonRuntime,
                SampleTime = (int)Math.Round(run.SampleTime),
                CaptureData = CaptureData(run.CaptureDataJson),
            });
        }

        return session;
    }

    /// <summary>
    /// The machine, out of the row rather than out of the stored JSON.
    /// </summary>
    /// <remarks>
    /// These are the fields a user may correct, and a correction is written to the row. Reading
    /// them back out of the capture would show the imported original and quietly undo the edit in
    /// every view that goes through the parsed model.
    /// </remarks>
    private static SessionInfo Info(StoredSession record) =>
        new()
        {
            GameName = record.GameName,
            ProcessName = record.ProcessName,
            Comment = record.Comment,
            Processor = record.Processor,
            Motherboard = record.Motherboard,
            SystemRam = record.SystemRam,
            GPU = record.Gpu,
            GPUCount = record.GpuCount?.ToString(),
            GpuCoreClock = record.GpuCoreClock?.ToString(),
            GpuMemoryClock = record.GpuMemoryClock?.ToString(),
            BaseDriverVersion = record.BaseDriverVersion,
            DriverPackage = record.DriverPackage,
            GPUDriverVersion = record.GpuDriverVersion,
            OS = record.Os,
            ApiInfo = record.ApiInfo,
            ResizableBar = Switch(record.ResizableBar),
            WinGameMode = Switch(record.WinGameMode),
            HAGS = Switch(record.Hags),
            PresentationMode = record.PresentationMode,
            ResolutionInfo = record.ResolutionInfo,
            CreationDate = record.CreatedAt,
        };

    private static SessionCaptureData? CaptureData(string? json)
    {
        if (string.IsNullOrEmpty(json))
        {
            return null;
        }

        try
        {
            return JsonConvert.DeserializeObject<SessionCaptureData>(json);
        }
        catch (JsonException)
        {
            // A run whose payload will not parse contributes nothing, the way a run without frame
            // data does. Failing the whole record would lose the runs that are fine.
            return null;
        }
    }

    /// <summary>Back into the three-state wording the capture format uses.</summary>
    private static string? Switch(bool? value) => value switch
    {
        true => "Enabled",
        false => "Disabled",
        null => null,
    };
}
