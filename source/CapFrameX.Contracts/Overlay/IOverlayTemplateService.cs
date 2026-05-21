using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Overlay
{
    public enum EGpuVendor
    {
        Nvidia,
        Amd,
        Intel,
        Unknown
    }

    public enum ECpuVendor
    {
        Intel,
        Amd,
        Unknown
    }

    public interface IOverlayTemplateService
    {
        /// <summary>
        /// Gets the detected GPU vendor based on hardware name.
        /// </summary>
        EGpuVendor DetectedGpuVendor { get; }

        /// <summary>
        /// Gets the detected CPU vendor based on hardware name.
        /// </summary>
        ECpuVendor DetectedCpuVendor { get; }

        /// <summary>
        /// Applies the specified template to the overlay entries.
        /// </summary>
        /// <param name="template">The template to apply.</param>
        /// <param name="entries">The overlay entries to modify.</param>
        void ApplyTemplate(EOverlayTemplate template, IEnumerable<IOverlayEntry> entries);

        /// <summary>
        /// Stores the current state of overlay entries for later revert.
        /// </summary>
        /// <param name="entries">The overlay entries to store.</param>
        void StoreCurrentState(IEnumerable<IOverlayEntry> entries);

        /// <summary>
        /// Gets the stored overlay entries.
        /// </summary>
        /// <returns></returns>
        IEnumerable<IOverlayEntry> GetStoredOverlayEntries();

        /// <summary>
        /// Checks if there is a stored state available for revert.
        /// </summary>
        bool HasStoredState { get; }
    }
}
