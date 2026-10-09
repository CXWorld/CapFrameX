namespace CapFrameX.OSD.Integration
{
    /// <summary>Independent of classic overlay visibility, grouping and formatting.</summary>
    public sealed class OverlayTelemetrySource
    {
        public string Id { get; init; }
        public string Identifier { get; init; }
        public string StableIdentifier { get; init; }
        public string Name { get; init; }
        public string HardwareName { get; init; }
        public string HardwareType { get; init; }
        public string SensorType { get; init; }
        public string Unit { get; init; }
        public string Category { get; init; }
        public string Subcategory { get; init; }
        /// <summary>Device identity for browsing and repeated sensor groups; never a display name alone.</summary>
        public string DeviceKey { get; init; }
        /// <summary>Optional device-qualified repeated sensor family, excluding aggregate readings.</summary>
        public string GroupKey { get; init; }
        public string GroupName { get; init; }
        /// <summary>Numeric core/thread order within a repeated sensor family.</summary>
        public int GroupOrder { get; init; }
        public bool IsText { get; init; }
        public bool IsAvailable { get; init; } = true;
        public bool IsHardwareSensor { get; init; }
        public bool IsStableIdentifierAmbiguous { get; init; }

        /// <summary>Optional preset role, assigned only to an unambiguous preferred source.</summary>
        public string SemanticKey { get; internal set; }
    }
}
