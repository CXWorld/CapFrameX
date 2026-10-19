using System;
using System.Collections.Generic;
using System.Linq;
using CapFrameX.Contracts.Localization;

namespace CapFrameX.Contracts.Overlay
{
    public sealed class OverlayDesignInfo
    {
        private readonly IReadOnlyList<LocalizedText> _description;

        public OverlayDesignInfo(string id, string name)
            : this(id, name, null, null)
        {
        }

        public OverlayDesignInfo(string id, string name, IReadOnlyList<LocalizedText> description, IReadOnlyList<OverlayDesignLink> links)
        {
            Id = id;
            Name = name;
            _description = description ?? Array.Empty<LocalizedText>();
            Links = links ?? Array.Empty<OverlayDesignLink>();
        }

        public string Id { get; }
        public string Name { get; }

        /// <summary>
        /// Paragraphs that describe a shipped design and credit its authors, in the current interface
        /// language. Empty for designs the user created.
        /// </summary>
        public IReadOnlyList<string> Description => _description.Select(paragraph => paragraph.Resolve()).ToArray();

        /// <summary>Web pages of the design's original authors.</summary>
        public IReadOnlyList<OverlayDesignLink> Links { get; }

        public bool HasDescription => _description.Count > 0 || Links.Count > 0;
    }

    public sealed class OverlayDesignLink
    {
        private readonly LocalizedText _text;

        public OverlayDesignLink(LocalizedText text, Uri uri)
        {
            _text = text ?? throw new ArgumentNullException(nameof(text));
            Uri = uri ?? throw new ArgumentNullException(nameof(uri));
        }

        public string Text => _text.Resolve();
        public Uri Uri { get; }
    }

    /// <summary>Chooses the saved design rendered by the CapFrameX OSD, independently of the editor selection.</summary>
    public interface IOverlayDesignService
    {
        IReadOnlyList<OverlayDesignInfo> Profiles { get; }
        string ActiveProfileId { get; }
        bool IsEnabled { get; }
        /// <summary>Tile overlays can be activated only with a CapFrameX renderer; RTSS uses Row overlay.</summary>
        bool CanActivate { get; }
        string Status { get; }
        event EventHandler Changed;
        void RefreshProfiles();
        /// <summary>Uses a saved tile profile and makes the overlay visible through its shared activation lifecycle.</summary>
        void Activate(string profileId);
        /// <summary>Deletes a saved profile. A successfully deleted active design returns to Row overlay without changing visibility or renderer.</summary>
        void Delete(string profileId);
        void Deactivate();
    }
}
