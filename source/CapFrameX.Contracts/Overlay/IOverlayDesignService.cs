using System;
using System.Collections.Generic;

namespace CapFrameX.Contracts.Overlay
{
    public sealed class OverlayDesignInfo
    {
        public OverlayDesignInfo(string id, string name)
        {
            Id = id;
            Name = name;
        }

        public string Id { get; }
        public string Name { get; }
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
