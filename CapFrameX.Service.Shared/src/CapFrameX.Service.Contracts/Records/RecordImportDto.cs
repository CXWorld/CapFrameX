namespace CapFrameX.Service.Contracts.Records;

/// <summary>A folder the service offers to import from.</summary>
/// <param name="Path">The folder.</param>
/// <param name="Origin">Where the suggestion came from, so the user can tell them apart.</param>
/// <param name="CaptureCount">How many capture files are in it, including its subfolders.</param>
public sealed record ImportSourceDto(string Path, string Origin, int CaptureCount);

/// <summary>What a first import could offer.</summary>
/// <param name="Sources">Folders worth importing from, each holding at least one capture.</param>
/// <param name="Offered">
/// Whether the user has already been asked. The frontend shows the first-run dialog once and never
/// again, whichever way they answered.
/// </param>
public sealed record ImportSourcesResponse(IReadOnlyList<ImportSourceDto> Sources, bool Offered);

/// <summary>Where to import from.</summary>
/// <param name="Path">A folder, or a single capture file.</param>
/// <param name="Recursive">Whether to look in the folder's subfolders too.</param>
public sealed record ImportRequest(string Path, bool Recursive = true);

/// <summary>What an import did.</summary>
/// <param name="Imported">Captures that entered the database.</param>
/// <param name="AlreadyKnown">Captures the database already held.</param>
/// <param name="Failed">Files that could not be read.</param>
/// <param name="Total">Files the import looked at.</param>
/// <param name="Errors">Why the failures failed, for the user to act on; capped in length.</param>
public sealed record ImportResultDto(
    int Imported,
    int AlreadyKnown,
    int Failed,
    int Total,
    IReadOnlyList<string> Errors);
