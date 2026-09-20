namespace CapFrameX.Service.Records;

/// <summary>
/// Decides what a scan of the capture folder means for the index.
/// </summary>
/// <remarks>
/// Kept apart from the file system and the database so the rules - what counts as changed, what a
/// version bump implies, what happens to a record whose file is gone - can be tested without
/// either.
/// </remarks>
public static class RecordIndexPlanner
{
    /// <summary>
    /// Version of the projection the indexer writes. Bump it when a summary gains or changes a
    /// field, and every record is re-read on the next scan without a schema migration.
    /// </summary>
    /// <remarks>
    /// Version 2 added the frame-rate metrics the record list shows.
    /// </remarks>
    public const int CurrentIndexVersion = 2;

    /// <summary>
    /// How two capture paths are compared. Windows hands the same file back under different
    /// casing depending on who asked; every other platform means two different files by it.
    /// </summary>
    public static StringComparer PathComparer { get; } =
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    /// <summary>Compares a folder listing against the index.</summary>
    /// <param name="filesOnDisk">What the capture folder currently holds.</param>
    /// <param name="indexed">What the index currently holds.</param>
    /// <param name="indexVersion">Version the indexer writes now.</param>
    public static RecordIndexPlan Plan(
        IReadOnlyCollection<RecordFile> filesOnDisk,
        IReadOnlyCollection<IndexedRecord> indexed,
        int indexVersion = CurrentIndexVersion)
    {
        ArgumentNullException.ThrowIfNull(filesOnDisk);
        ArgumentNullException.ThrowIfNull(indexed);

        // Rows without a source file were recorded by the service itself. The folder says nothing
        // about them, so they take no part in the comparison at all.
        var byPath = new Dictionary<string, IndexedRecord>(PathComparer);
        foreach (var record in indexed)
        {
            if (!string.IsNullOrEmpty(record.Path))
            {
                byPath[record.Path] = record;
            }
        }

        var added = new List<RecordFile>();
        var updated = new List<RecordFile>();
        var seen = new HashSet<string>(PathComparer);

        foreach (var file in filesOnDisk)
        {
            seen.Add(file.Path);

            if (!byPath.TryGetValue(file.Path, out var record))
            {
                added.Add(file);
            }
            else if (HasChanged(file, record, indexVersion))
            {
                updated.Add(file);
            }
        }

        var removed = new List<IndexedRecord>();
        foreach (var record in byPath.Values)
        {
            if (!seen.Contains(record.Path))
            {
                removed.Add(record);
            }
        }

        return new RecordIndexPlan(added, updated, removed);
    }

    private static bool HasChanged(RecordFile file, IndexedRecord record, int indexVersion)
    {
        if (file.Size != record.Size || file.ModifiedUtc != record.ModifiedUtc)
        {
            return true;
        }

        // Only rows behind the current projection are re-read. A row written by a newer service
        // sharing this database is left as it is rather than downgraded.
        return record.IndexVersion < indexVersion;
    }
}
