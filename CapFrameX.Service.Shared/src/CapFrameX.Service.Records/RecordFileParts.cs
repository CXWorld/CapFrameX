using Newtonsoft.Json.Linq;

namespace CapFrameX.Service.Records;

/// <summary>One run's payload, exactly as it stood in the capture file.</summary>
/// <param name="CaptureData">Frame arrays.</param>
/// <param name="SensorData">Hardware readings.</param>
/// <param name="RtssFrameTimes">RTSS frame times.</param>
/// <param name="PmdGpuPower">PMD graphics power.</param>
/// <param name="PmdCpuPower">PMD processor power.</param>
/// <param name="PmdSystemPower">PMD system power.</param>
public sealed record RecordRunParts(
    string? CaptureData,
    string? SensorData,
    string? RtssFrameTimes,
    string? PmdGpuPower,
    string? PmdCpuPower,
    string? PmdSystemPower);

/// <summary>
/// The parts of a capture file that are stored rather than interpreted.
/// </summary>
/// <remarks>
/// Taken as raw JSON out of the file rather than serialised back from the parsed model. The 1.x
/// model reads several of these through converters that are lossy in one direction - sensor data
/// most of all - so a round trip through it would quietly store something other than what the user
/// captured. Text in, text out: whatever the file said is what the database holds, and what comes
/// back out parses exactly as the file did.
/// </remarks>
public static class RecordFileParts
{
    /// <summary>Reads the per-run payloads out of a capture file's text.</summary>
    /// <param name="content">The file's text.</param>
    /// <returns>One entry per run, in the order the file lists them.</returns>
    public static IReadOnlyList<RecordRunParts> Read(string content)
    {
        ArgumentNullException.ThrowIfNull(content);

        var parts = new List<RecordRunParts>();

        if (JToken.Parse(content) is not JObject root || root["Runs"] is not JArray runs)
        {
            return parts;
        }

        foreach (var run in runs)
        {
            parts.Add(new RecordRunParts(
                CaptureData: Text(run["CaptureData"]),
                SensorData: Text(run["SensorData2"]) ?? Text(run["SensorData"]),
                RtssFrameTimes: Text(run["RTSSFrameTimes"]),
                PmdGpuPower: Text(run["PmdGpuPower"]),
                PmdCpuPower: Text(run["PmdCpuPower"]),
                PmdSystemPower: Text(run["PmdSystemPower"])));
        }

        return parts;
    }

    private static string? Text(JToken? token) =>
        token is null || token.Type == JTokenType.Null ? null : token.ToString(Newtonsoft.Json.Formatting.None);
}
