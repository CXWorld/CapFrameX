namespace CapFrameX.Service.Contracts.Records;

public sealed record RecordsListResponse(IReadOnlyList<RecordSummaryDto> Records);
