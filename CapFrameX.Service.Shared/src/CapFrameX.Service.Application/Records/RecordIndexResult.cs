namespace CapFrameX.Service.Application.Records;

/// <summary>What one scan did.</summary>
/// <param name="Added">Captures that entered the index.</param>
/// <param name="Updated">Captures that were re-read.</param>
/// <param name="Removed">Indexed captures whose file is gone.</param>
/// <param name="Failed">Files that could not be read, and are left for the next scan.</param>
public readonly record struct RecordIndexResult(int Added, int Updated, int Removed, int Failed)
{
    /// <summary>Whether the index already matched the folder.</summary>
    public bool IsEmpty => Added == 0 && Updated == 0 && Removed == 0;
}
