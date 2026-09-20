using System.Text;
using CapFrameX.Data.Session.Contracts;
using CapFrameX.Service.Contracts.Records;
using Newtonsoft.Json;

namespace CapFrameX.Service.Records;

/// <summary>
/// Writes a CapFrameX capture file back.
/// </summary>
/// <remarks>
/// The capture file is the record, so an edit belongs in it rather than beside it in a database:
/// the user's correction has to survive being copied to another machine, and CapFrameX 1.x has to
/// read it. The format is the one 1.x writes - plain Newtonsoft serialisation of the same model -
/// for the same reason the reader uses that model rather than a new one.
/// </remarks>
public sealed class RecordFileWriter
{
    /// <summary>Extension of the file a half-finished write leaves behind.</summary>
    /// <remarks>
    /// Not <c>.json</c>, so the indexer's scan cannot pick a partial file up as a capture.
    /// </remarks>
    public const string TemporaryExtension = ".tmp";

    /// <summary>
    /// Applies a patch to a parsed capture.
    /// </summary>
    /// <param name="session">The parsed capture, changed in place.</param>
    /// <param name="edit">What to change; a field left out is left alone.</param>
    /// <returns>Whether anything actually changed.</returns>
    public static bool Apply(ISession session, RecordEdit edit)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(edit);

        var info = session.Info;

        if (info is null)
        {
            return false;
        }

        var changed = false;

        changed |= Set(edit.GameName, info.GameName, value => info.GameName = value);
        changed |= Set(edit.Comment, info.Comment, value => info.Comment = value);
        changed |= Set(edit.Processor, info.Processor, value => info.Processor = value);
        changed |= Set(edit.Gpu, info.GPU, value => info.GPU = value);
        changed |= Set(edit.SystemRam, info.SystemRam, value => info.SystemRam = value);
        changed |= Set(edit.Motherboard, info.Motherboard, value => info.Motherboard = value);
        changed |= Set(edit.ResolutionInfo, info.ResolutionInfo, value => info.ResolutionInfo = value);

        return changed;
    }

    /// <summary>
    /// Writes a capture to its file.
    /// </summary>
    /// <remarks>
    /// Serialised into memory first and moved into place afterwards, because this overwrites a file
    /// the user cannot get back: a failure halfway through a direct write would leave a truncated
    /// capture where a whole one was.
    /// </remarks>
    /// <param name="path">Where the capture lives.</param>
    /// <param name="session">The capture to write.</param>
    /// <param name="cancellationToken">Cancels the write.</param>
    public async Task SaveAsync(string path, ISession session, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(session);

        var content = JsonConvert.SerializeObject(session);
        var temporary = path + TemporaryExtension;

        try
        {
            await File.WriteAllTextAsync(temporary, content, new UTF8Encoding(false), cancellationToken);

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            Delete(temporary);

            throw;
        }
    }

    private static void Delete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The original is intact either way; a leftover temporary file is not worth throwing
            // over the failure that caused it.
        }
    }

    private static bool Set(string? wanted, string? current, Action<string> assign)
    {
        if (wanted is null || string.Equals(wanted, current, StringComparison.Ordinal))
        {
            return false;
        }

        assign(wanted);

        return true;
    }
}
