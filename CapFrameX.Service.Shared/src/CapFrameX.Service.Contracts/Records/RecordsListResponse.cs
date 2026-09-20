namespace CapFrameX.Service.Contracts.Records;

/// <summary>One page of the record list.</summary>
/// <param name="Records">The captures on this page, newest first.</param>
/// <param name="Total">How many captures match, across all pages.</param>
public sealed record RecordsListResponse(IReadOnlyList<RecordSummaryDto> Records, int Total);
