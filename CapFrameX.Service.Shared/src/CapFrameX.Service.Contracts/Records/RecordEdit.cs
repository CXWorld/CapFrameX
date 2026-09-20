namespace CapFrameX.Service.Contracts.Records;

/// <summary>
/// The fields of a capture a user may correct.
/// </summary>
/// <remarks>
/// Exactly the set CapFrameX 1.x has always allowed editing, because the change is written back
/// into the capture file and both generations read the same files. A field left out is left alone,
/// which is what makes this a patch; an empty string clears it.
/// </remarks>
/// <param name="GameName">What the game is called.</param>
/// <param name="Comment">The user's note about this capture.</param>
/// <param name="Processor">CPU, where the detected name is wrong or unhelpful.</param>
/// <param name="Gpu">Graphics card.</param>
/// <param name="SystemRam">Memory.</param>
/// <param name="Motherboard">Mainboard.</param>
/// <param name="ResolutionInfo">Display resolution.</param>
public sealed record RecordEdit(
    string? GameName = null,
    string? Comment = null,
    string? Processor = null,
    string? Gpu = null,
    string? SystemRam = null,
    string? Motherboard = null,
    string? ResolutionInfo = null)
{
    /// <summary>Whether the patch asks for anything at all.</summary>
    public bool IsEmpty =>
        GameName is null &&
        Comment is null &&
        Processor is null &&
        Gpu is null &&
        SystemRam is null &&
        Motherboard is null &&
        ResolutionInfo is null;
}
