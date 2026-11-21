namespace CapFrameX.Contracts.Overlay
{
    /// <summary>
    /// Names the upscaler and the frame generation a game runs, with their versions. Only what is
    /// proven to be in use is named: a runtime that is merely loaded into the process reports
    /// nothing. The proof comes from the in-game hook, so both values are null for a process the
    /// hook is not attached to.
    /// </summary>
    public interface IUpscalingTechnologyVersionService
    {
        /// <summary>
        /// The upscaler the process dispatched within the last seconds, as "FSR 3.1.5". A trailing
        /// "(DLL)" marks a version that is the default of the game's library instead of the answer
        /// of the dispatched context. Null when no upscaler is known to run.
        /// </summary>
        /// <param name="processId">The process the in-game hook is attached to.</param>
        /// <returns>The display text, or null.</returns>
        string GetUpscalerVersion(int processId);

        /// <summary>
        /// The frame generation the process has switched on, as "DLSS 310.7.130", "FSR 3.1.6" or
        /// "XeSS 1.3.1". Null while frame generation is off or its version is unknown.
        /// </summary>
        /// <param name="processId">The process the in-game hook is attached to.</param>
        /// <returns>The display text, or null.</returns>
        string GetFrameGenerationVersion(int processId);

        /// <summary>
        /// How many frames reach the display for every frame the process renders while its frame
        /// generation is switched on, as "2x", "3x" or "4x". Measured over the last seconds, not
        /// read from a setting: "1x" while nothing is generated, one decimal ("2.5x") for a factor
        /// between two whole ones. While the hook cannot tell whether frame generation is on, a
        /// factor is only named when the measurement shows generated frames. Null while frame
        /// generation is off or the factor cannot be measured yet. Call it regularly: the
        /// measurement spans the calls.
        /// </summary>
        /// <param name="processId">The process the in-game hook is attached to.</param>
        /// <returns>The display text, or null.</returns>
        string GetFrameGenerationFactor(int processId);
    }
}
