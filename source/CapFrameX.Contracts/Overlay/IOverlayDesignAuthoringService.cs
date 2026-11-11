using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapFrameX.Contracts.Overlay
{
    /// <summary>
    /// Creates and edits saved tile overlay profiles on behalf of automation clients (the MCP
    /// server). Profiles are exchanged as tile documents: the tile editor's own model, so every
    /// profile written here opens in the editor and passes the same validation.
    /// </summary>
    public interface IOverlayDesignAuthoringService
    {
        /// <summary>Saved profiles plus the overlay and editor state that decides what a change affects.</summary>
        Task<OverlayDesignLibraryState> GetLibraryAsync();

        /// <summary>Returns one profile as a tile document. <paramref name="profile"/> is an ID or a name.</summary>
        Task<OverlayDesignProfileContent> GetProfileAsync(string profile);

        /// <summary>
        /// Creates a profile from exactly one source: a tile document, a gallery template, or an
        /// existing profile. Template roles and sensor groups are bound to this machine's sensors.
        /// </summary>
        Task<OverlayDesignProfileContent> CreateProfileAsync(string name, string documentJson, string templateId, string copyFromProfile);

        /// <summary>Replaces the whole tile document of a profile. The profile keeps its name.</summary>
        Task<OverlayDesignProfileContent> UpdateProfileAsync(string profile, string documentJson);

        /// <summary>Applies a JSON array of edit operations atomically: all of them or none are saved.</summary>
        Task<OverlayDesignProfileContent> EditProfileAsync(string profile, string operationsJson);

        Task<OverlayDesignProfileInfo> RenameProfileAsync(string profile, string name);

        /// <summary>Deletes a profile. The last remaining profile cannot be deleted.</summary>
        Task<OverlayDesignLibraryState> DeleteProfileAsync(string profile);

        /// <summary>Shows the profile on the CapFrameX OSD (in-game or hook-free renderer).</summary>
        Task<OverlayDesignLibraryState> ActivateProfileAsync(string profile);

        /// <summary>Returns the CapFrameX OSD to the classic row overlay.</summary>
        Task<OverlayDesignLibraryState> DeactivateProfileAsync();

        /// <summary>
        /// Renders a saved profile, or an unsaved tile document, with the native OSD renderer.
        /// Exactly one of <paramref name="profile"/> and <paramref name="documentJson"/> is used.
        /// </summary>
        Task<OverlayDesignPreview> RenderPreviewAsync(string profile, string documentJson, bool liveValues);

        /// <summary>
        /// Opens the tile editor window, optionally on one profile. Returns false when the editor
        /// kept another profile because it holds unsaved changes the user has not resolved.
        /// </summary>
        Task<bool> OpenEditorAsync(string profile);

        /// <summary>Telemetry readings a tile can bind to, and the repeated sensor families a group tile can show.</summary>
        Task<OverlayDesignSourceCatalog> GetSourcesAsync();

        IReadOnlyList<OverlayDesignTemplateInfo> GetTemplates();

        /// <summary>The authoring reference: tile document format, widget kinds, fields, ranges and workflow.</summary>
        string GetDesignGuide();
    }

    /// <summary>Rejected input. The message is meant for the client and names the offending field or tile.</summary>
    public sealed class OverlayDesignAuthoringException : Exception
    {
        public OverlayDesignAuthoringException(string message) : base(message)
        {
        }

        public OverlayDesignAuthoringException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }

    public sealed class OverlayDesignProfileInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public DateTime ModifiedUtc { get; set; }
        /// <summary>"canvas" or "grid".</summary>
        public string Layout { get; set; }
        /// <summary>Every tile, including the ones nested in containers.</summary>
        public int TileCount { get; set; }
        /// <summary>The CapFrameX OSD currently renders this profile.</summary>
        public bool IsActiveInOverlay { get; set; }
        /// <summary>The open tile editor shows this profile.</summary>
        public bool IsOpenInEditor { get; set; }
        /// <summary>A shipped starter profile that nobody has changed yet.</summary>
        public bool IsUnmodifiedStarter { get; set; }
        /// <summary>Set when the stored design cannot be opened as a tile document.</summary>
        public string LoadError { get; set; }
    }

    public sealed class OverlayDesignLibraryState
    {
        public IReadOnlyList<OverlayDesignProfileInfo> Profiles { get; set; } = Array.Empty<OverlayDesignProfileInfo>();
        /// <summary>The profile the CapFrameX OSD renders, or null for the classic row overlay.</summary>
        public string ActiveProfileId { get; set; }
        /// <summary>False while RTSS or Off is the selected renderer: tile profiles need a CapFrameX renderer.</summary>
        public bool CanActivate { get; set; }
        public string OverlayStatus { get; set; }
        public bool EditorOpen { get; set; }
        public string EditorProfileId { get; set; }
        /// <summary>Unsaved editor changes block every library change until the user saves or discards them.</summary>
        public bool EditorHasUnsavedChanges { get; set; }
        public string LibraryWarning { get; set; }
    }

    public sealed class OverlayDesignProfileContent
    {
        public OverlayDesignProfileInfo Profile { get; set; }
        /// <summary>The tile document as JSON text.</summary>
        public string DocumentJson { get; set; }
        /// <summary>Non-fatal findings, for example keys that match no reading on this machine.</summary>
        public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
        /// <summary>Set by write operations: the open tile editor reloaded the library.</summary>
        public bool EditorReloaded { get; set; }
        /// <summary>Set by write operations: the OSD renders this profile and picked up the change.</summary>
        public bool OverlayUpdated { get; set; }
    }

    public sealed class OverlayDesignPreview
    {
        public byte[] PngBytes { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        /// <summary>Rendered pixels per design pixel; below 1 when a large design had to be reduced.</summary>
        public double Scale { get; set; }
        /// <summary>"live" (current sensor readings) or "demo" (synthetic values).</summary>
        public string Values { get; set; }
        public string ProfileName { get; set; }
        public IReadOnlyList<OverlayDesignPreviewTile> Tiles { get; set; } = Array.Empty<OverlayDesignPreviewTile>();
        public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
    }

    /// <summary>Where a top-level tile landed in the preview image, in image pixels.</summary>
    public sealed class OverlayDesignPreviewTile
    {
        public string Path { get; set; }
        public string Kind { get; set; }
        public string Label { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public sealed class OverlayDesignSourceCatalog
    {
        public IReadOnlyList<OverlayDesignSourceInfo> Sources { get; set; } = Array.Empty<OverlayDesignSourceInfo>();
        public IReadOnlyList<OverlayDesignSourceGroupInfo> Groups { get; set; } = Array.Empty<OverlayDesignSourceGroupInfo>();
    }

    public sealed class OverlayDesignSourceInfo
    {
        /// <summary>The value for a tile's "key" or "secondaryKey".</summary>
        public string Key { get; set; }
        public string Name { get; set; }
        public string Unit { get; set; }
        public string Category { get; set; }
        public string Subcategory { get; set; }
        public string Device { get; set; }
        /// <summary>Portable role such as "gpuTemp"; usable as a key on every machine that has an unambiguous match.</summary>
        public string SemanticKey { get; set; }
        public bool IsText { get; set; }
        public bool IsAvailable { get; set; }
        /// <summary>The sensor family (see <see cref="OverlayDesignSourceGroupInfo.Key"/>) this reading belongs to.</summary>
        public string GroupKey { get; set; }
    }

    public sealed class OverlayDesignSourceGroupInfo
    {
        /// <summary>The value for a group tile's "groupKey"; the members are filled in automatically.</summary>
        public string Key { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Device { get; set; }
        public string Unit { get; set; }
        public int MemberCount { get; set; }
    }

    public sealed class OverlayDesignTemplateInfo
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public string Description { get; set; }
        public int Columns { get; set; }
        public int TileCount { get; set; }
    }
}
