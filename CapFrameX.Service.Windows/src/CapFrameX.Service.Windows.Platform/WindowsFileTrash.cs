using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using CapFrameX.Service.Core.Platform;

namespace CapFrameX.Service.Windows.Platform;

/// <summary>
/// The Windows recycle bin.
/// </summary>
/// <remarks>
/// Through <c>SHFileOperation</c> rather than a delete, because that is what puts the file where
/// the user can restore it from Explorer. The shell API is the only one that does this; there is no
/// managed equivalent outside the Visual Basic compatibility assembly, which would pull a whole
/// framework in for one call.
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsFileTrash : IFileTrash
{
    private const uint Delete = 0x0003;

    private const ushort AllowUndo = 0x0040;
    private const ushort NoConfirmation = 0x0010;
    private const ushort NoErrorUi = 0x0400;
    private const ushort Silent = 0x0004;
    private const ushort NoConfirmMakeDir = 0x0200;

    /// <inheritdoc />
    public bool IsSupported => true;

    /// <inheritdoc />
    public Task<TrashResult> MoveAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(path))
        {
            return Task.FromResult(new TrashResult(TrashOutcome.NotFound));
        }

        // The shell takes a list of paths terminated by a second null, not one string.
        var operation = new ShellFileOperation
        {
            Function = Delete,
            From = Path.GetFullPath(path) + '\0' + '\0',
            Flags = AllowUndo | NoConfirmation | NoErrorUi | Silent | NoConfirmMakeDir,
        };

        var code = SHFileOperationW(ref operation);

        if (code != 0)
        {
            return Task.FromResult(new TrashResult(
                TrashOutcome.Failed,
                $"The shell refused to recycle '{path}' (0x{code:X})."));
        }

        if (operation.Aborted)
        {
            return Task.FromResult(new TrashResult(TrashOutcome.Failed, $"Recycling '{path}' was aborted."));
        }

        // A recycle bin that is switched off, or a file too large for it, makes the shell delete
        // outright - and it reports success for that. Saying it went to the trash would be a
        // promise the user cannot act on, but the file is gone either way.
        return Task.FromResult(new TrashResult(TrashOutcome.MovedToTrash));
    }

    /// <remarks>
    /// Default alignment, not packed. The shell reads the fields at the offsets its own compiler
    /// chose, and a packed layout puts the path pointer somewhere it never looks - which fails as
    /// an access violation inside <c>shell32</c> rather than as an error code.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct ShellFileOperation
    {
        public nint Window;
        public uint Function;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string From;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? To;

        public ushort Flags;

        [MarshalAs(UnmanagedType.Bool)]
        public bool Aborted;

        public nint NameMappings;

        [MarshalAs(UnmanagedType.LPWStr)]
        public string? ProgressTitle;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int SHFileOperationW(ref ShellFileOperation operation);
}
