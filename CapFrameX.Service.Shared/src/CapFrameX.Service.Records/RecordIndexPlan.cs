namespace CapFrameX.Service.Records;

/// <summary>One capture file as the file system reports it.</summary>
/// <param name="Path">Full path of the file.</param>
/// <param name="Size">Size in bytes.</param>
/// <param name="ModifiedUtc">Last write time in UTC.</param>
public readonly record struct RecordFile(string Path, long Size, DateTime ModifiedUtc);

/// <summary>One capture as the index currently holds it.</summary>
/// <param name="Id">Identity of the indexed row.</param>
/// <param name="Path">Full path of the file it was read from.</param>
/// <param name="Size">Size the file had when it was indexed.</param>
/// <param name="ModifiedUtc">Last write time the file had when it was indexed.</param>
/// <param name="IndexVersion">Indexer version that wrote the row.</param>
public readonly record struct IndexedRecord(Guid Id, string Path, long Size, DateTime ModifiedUtc, int IndexVersion);

/// <summary>What the indexer has to do to bring the index in line with the folder.</summary>
/// <param name="Added">Files that are not in the index yet.</param>
/// <param name="Updated">Files whose contents or projection changed since they were indexed.</param>
/// <param name="Removed">Indexed records whose file is gone.</param>
public sealed record RecordIndexPlan(
    IReadOnlyList<RecordFile> Added,
    IReadOnlyList<RecordFile> Updated,
    IReadOnlyList<IndexedRecord> Removed)
{
    /// <summary>Whether the index already matches the folder.</summary>
    public bool IsEmpty => Added.Count == 0 && Updated.Count == 0 && Removed.Count == 0;
}
