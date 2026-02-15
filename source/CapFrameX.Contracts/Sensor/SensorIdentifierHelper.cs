namespace CapFrameX.Contracts.Sensor
{
    public static class SensorIdentifierHelper
    {
        /// <summary>
        /// Builds a version-stable identifier from hardware name, sensor type, and sensor name.
        /// Format: "{HardwareName}/{sensorTypeLowercase}/{sensorName}"
        /// Returns null if hardwareName is null or empty (e.g., for non-hardware entries).
        /// </summary>
        public static string BuildStableIdentifier(string hardwareName, string sensorType, string sensorName)
        {
            if (string.IsNullOrEmpty(hardwareName))
                return null;

            var sensorTypeLower = sensorType?.ToLowerInvariant() ?? string.Empty;
            return $"{hardwareName}/{sensorTypeLower}/{sensorName}";
        }

        /// <summary>
        /// Builds a stable identifier from an ISensorEntry.
        /// Returns null if the entry has no HardwareName.
        /// </summary>
        public static string BuildStableIdentifier(ISensorEntry entry)
        {
            return BuildStableIdentifier(entry.HardwareName, entry.SensorType, entry.Name);
        }
    }
}
