using System;

namespace CapFrameX.OSD.Integration
{
    public enum OverlayDesignUnsavedChangesDecision
    {
        Cancel,
        Save,
        Discard
    }

    /// <summary>UI-independent edit lifecycle. Failed or cancelled transitions retain the working document.</summary>
    public sealed class OverlayDesignProfileSession
    {
        private readonly OverlayDesignProfileStore _store;
        private string _baseline;

        public OverlayDesignProfileSession(OverlayDesignProfileStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public OverlayDesignProfile ActiveProfile { get; private set; }
        public string WorkingTemplateJson { get; private set; }
        public bool HasValidationErrors { get; private set; }
        public bool IsDirty => HasValidationErrors || !string.Equals(WorkingTemplateJson, _baseline, StringComparison.Ordinal);

        public void Initialize(string defaultName, string defaultJson)
        {
            if (ActiveProfile != null)
                throw new InvalidOperationException("The overlay profile session is already initialized.");
            Load(_store.Profiles.Count == 0 ? _store.Create(defaultName, defaultJson) : _store.Get(_store.ActiveProfileId));
        }

        public void SetWorkingTemplate(string json)
        {
            EnsureInitialized();
            // Leave the last valid document available to the UI while an invalid edit is present.
            try
            {
                WorkingTemplateJson = _store.Canonicalize(json);
                HasValidationErrors = false;
            }
            catch
            {
                HasValidationErrors = true;
                throw;
            }
        }

        public void MarkInvalidEdit()
        {
            EnsureInitialized();
            HasValidationErrors = true;
        }

        public void Save()
        {
            EnsureValid();
            var saved = _store.Save(ActiveProfile.Id, WorkingTemplateJson);
            ActiveProfile = saved;
            WorkingTemplateJson = saved.DesignJson;
            _baseline = saved.DesignJson;
        }

        public bool TrySwitch(string id, OverlayDesignUnsavedChangesDecision decision)
        {
            EnsureInitialized();
            var next = _store.Get(id);
            if (ActiveProfile.Id == id)
                return true;
            if (!ResolveUnsaved(decision))
                return false;
            _store.SetActive(id);
            Load(next);
            return true;
        }

        public bool TryClose(OverlayDesignUnsavedChangesDecision decision)
        {
            EnsureInitialized();
            if (!ResolveUnsaved(decision))
                return false;
            if (decision == OverlayDesignUnsavedChangesDecision.Discard)
                Load(_store.Get(ActiveProfile.Id));
            return true;
        }

        public bool Create(string name, string json, OverlayDesignUnsavedChangesDecision decision)
        {
            EnsureInitialized();
            if (!ResolveUnsaved(decision))
                return false;
            Load(_store.Create(name, json));
            return true;
        }

        /// <summary>Saves the working design as a new profile, keeping the original profile unchanged.</summary>
        public void Duplicate(string name)
        {
            EnsureValid();
            Load(_store.Create(name, WorkingTemplateJson));
        }

        public void Rename(string name)
        {
            EnsureInitialized();
            string working = _store.WithName(WorkingTemplateJson, name);
            ActiveProfile = _store.Rename(ActiveProfile.Id, name);
            WorkingTemplateJson = working;
            _baseline = ActiveProfile.DesignJson;
        }

        public bool DeleteActive(OverlayDesignUnsavedChangesDecision decision)
        {
            EnsureInitialized();
            if (_store.Profiles.Count < 2)
                throw new InvalidOperationException("Keep at least one overlay profile. Create another profile before deleting this one.");
            if (!ResolveUnsaved(decision))
                return false;
            _store.Delete(ActiveProfile.Id);
            Load(_store.Get(_store.ActiveProfileId));
            return true;
        }

        private bool ResolveUnsaved(OverlayDesignUnsavedChangesDecision decision)
        {
            if (!Enum.IsDefined(decision))
                throw new ArgumentOutOfRangeException(nameof(decision));
            if (!IsDirty)
                return true;
            switch (decision)
            {
                case OverlayDesignUnsavedChangesDecision.Save:
                    Save();
                    return true;
                case OverlayDesignUnsavedChangesDecision.Discard:
                    return true;
                default:
                    return false;
            }
        }

        private void Load(OverlayDesignProfile profile)
        {
            ActiveProfile = profile;
            WorkingTemplateJson = profile.DesignJson;
            _baseline = profile.DesignJson;
            HasValidationErrors = false;
        }

        private void EnsureInitialized()
        {
            if (ActiveProfile == null)
                throw new InvalidOperationException("Initialize the overlay profile session first.");
        }

        private void EnsureValid()
        {
            EnsureInitialized();
            if (HasValidationErrors)
                throw new InvalidOperationException("Fix the invalid design values before saving, or discard your changes.");
        }
    }
}
