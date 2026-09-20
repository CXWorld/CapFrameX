namespace CapFrameX.Service.Contracts.Records;

/// <summary>
/// What a scan of the capture folder changed, so the frontend can reload the list instead of
/// polling for it.
/// </summary>
/// <param name="Added">Captures that entered the index.</param>
/// <param name="Updated">Captures that were re-read.</param>
/// <param name="Removed">Captures whose file is gone.</param>
public sealed record RecordsChangedDto(int Added, int Updated, int Removed);
