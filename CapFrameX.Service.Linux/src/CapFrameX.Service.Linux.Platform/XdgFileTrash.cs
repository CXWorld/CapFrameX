using System.Globalization;
using System.Text;
using CapFrameX.Service.Core.Platform;

namespace CapFrameX.Service.Linux.Platform;

/// <summary>
/// The freedesktop trash, which is what every Linux file manager shows as "Trash".
/// </summary>
/// <remarks>
/// There is no system call for this: the trash is a convention about two directories, and the
/// specification is what makes a file the service removed restorable from Nautilus, Dolphin or
/// <c>gio trash --restore</c>. The order matters - the info file is created first and exclusively,
/// which is how two programs trashing the same name at the same time cannot overwrite each other's
/// record of where the file came from.
/// </remarks>
public sealed class XdgFileTrash : IFileTrash
{
    /// <summary>Extension of the file that records where a trashed file came from.</summary>
    public const string InfoExtension = ".trashinfo";

    private readonly string _trashHome;

    /// <summary>Creates the trash.</summary>
    /// <param name="read">Reads an environment variable; defaults to the process environment.</param>
    /// <param name="homeDirectory">The user's home; defaults to the one the platform reports.</param>
    public XdgFileTrash(Func<string, string?>? read = null, string? homeDirectory = null)
    {
        var readVariable = read ?? Environment.GetEnvironmentVariable;
        var home = homeDirectory
            ?? readVariable("HOME")
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var data = Absolute(readVariable("XDG_DATA_HOME"))
            ?? Path.Combine(home, ".local", "share");

        _trashHome = Path.Combine(data, "Trash");
    }

    /// <summary>Where trashed files and their records are kept.</summary>
    public string TrashHome => _trashHome;

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public async Task<TrashResult> MoveAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        var full = Path.GetFullPath(path);

        if (!File.Exists(full))
        {
            return new TrashResult(TrashOutcome.NotFound);
        }

        var files = Path.Combine(_trashHome, "files");
        var info = Path.Combine(_trashHome, "info");

        try
        {
            Directory.CreateDirectory(files);
            Directory.CreateDirectory(info);

            var name = Reserve(info, Path.GetFileName(full), out var infoPath);

            try
            {
                await File.WriteAllTextAsync(infoPath, Record(full), new UTF8Encoding(false), cancellationToken);

                File.Move(full, Path.Combine(files, name));
            }
            catch
            {
                // The record without the file it describes would show up in every file manager as
                // an entry that cannot be restored.
                TryDelete(infoPath);

                throw;
            }

            return new TrashResult(TrashOutcome.MovedToTrash);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return new TrashResult(TrashOutcome.Failed, $"'{path}' could not be trashed: {exception.Message}");
        }
    }

    /// <summary>
    /// Takes a name in the trash that nobody else holds.
    /// </summary>
    /// <remarks>
    /// The info file is created with <see cref="FileMode.CreateNew"/> for exactly this: two
    /// programs trashing files of the same name at the same moment both get a name of their own,
    /// because the file system decides rather than a check-then-write.
    /// </remarks>
    private static string Reserve(string info, string fileName, out string infoPath)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        var extension = Path.GetExtension(fileName);

        for (var attempt = 0; ; attempt++)
        {
            var name = attempt == 0
                ? fileName
                : $"{stem}.{attempt}{extension}";

            infoPath = Path.Combine(info, name + InfoExtension);

            try
            {
                using var reserved = new FileStream(infoPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);

                return name;
            }
            catch (IOException) when (File.Exists(infoPath))
            {
                // Taken; try the next name.
            }
        }
    }

    /// <summary>
    /// The record a file manager reads to offer "restore".
    /// </summary>
    /// <remarks>
    /// The path is percent-encoded as the specification requires, and the deletion date is local
    /// time without a zone, which is what the specification says and what other implementations
    /// write.
    /// </remarks>
    private static string Record(string originalPath) =>
        "[Trash Info]\n" +
        $"Path={Encode(originalPath)}\n" +
        $"DeletionDate={DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)}\n";

    private static string Encode(string path)
    {
        var encoded = new StringBuilder(path.Length);

        foreach (var value in Encoding.UTF8.GetBytes(path))
        {
            var character = (char)value;

            // Unreserved characters plus the separator, which stays readable in every file manager.
            if (char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or '~' or '/')
            {
                encoded.Append(character);
            }
            else
            {
                encoded.Append('%').Append(value.ToString("X2", CultureInfo.InvariantCulture));
            }
        }

        return encoded.ToString();
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Nothing better to do; the caller already has the failure that brought us here.
        }
    }

    private static string? Absolute(string? value) =>
        !string.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value) ? value : null;
}
