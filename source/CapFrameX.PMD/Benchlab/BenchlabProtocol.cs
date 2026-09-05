using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;

namespace CapFrameX.PMD.Benchlab
{
    internal static class BenchlabProtocol
    {
        public const string DiscoveryPipeName = "BenchlabDiscovery";
        public const string ListDevicesCommand = "ListDevices";
        public const string GetUpdatedSensorListCommand = "GetUpdatedSensorList";

        public static IList<BenchlabDeviceInfo> DeserializeDevices(string json)
        {
            var devices = JsonConvert.DeserializeObject<List<BenchlabDeviceInfo>>(RemoveByteOrderMark(json));
            return devices ?? new List<BenchlabDeviceInfo>();
        }

        public static BenchlabDeviceInfo SelectDevice(IList<BenchlabDeviceInfo> devices, string preferredDeviceId = null)
        {
            if (devices == null)
            {
                return null;
            }

            var connectedDevices = devices
                .Where(device => device != null
                    && device.IsConnected
                    && !string.IsNullOrWhiteSpace(device.PipeName))
                .ToList();

            if (!string.IsNullOrWhiteSpace(preferredDeviceId))
            {
                return connectedDevices.FirstOrDefault(device =>
                    string.Equals(device.DeviceId, preferredDeviceId, StringComparison.OrdinalIgnoreCase));
            }

            return connectedDevices.FirstOrDefault();
        }

        public static IList<Sensor> DeserializeSensors(string json)
        {
            var response = JsonConvert.DeserializeObject<BenchlabSensorResponse>(RemoveByteOrderMark(json));
            if (response == null)
            {
                throw new InvalidDataException("The BENCHLAB service returned an empty telemetry response.");
            }

            if (!string.Equals(response.Status, "CONNECTED", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"The BENCHLAB device is not connected (status: {response.Status ?? "unknown"}).");
            }

            if (!response.SensorsUpdated)
            {
                throw new InvalidDataException("The BENCHLAB service could not refresh the sensor data.");
            }

            if (response.Sensors == null || response.Sensors.Count == 0)
            {
                throw new InvalidDataException("The BENCHLAB service returned no sensors.");
            }

            return response.Sensors;
        }

        public static bool TryGetPowerSensorIndices(
            IList<Sensor> sensors,
            out int cpuPowerSensorIndex,
            out int gpuPowerSensorIndex,
            out int mainboardPowerSensorIndex,
            out int systemPowerSensorIndex)
        {
            cpuPowerSensorIndex = FindSensorIndex(sensors, "CPU_P");
            gpuPowerSensorIndex = FindSensorIndex(sensors, "GPU_P");
            mainboardPowerSensorIndex = FindSensorIndex(sensors, "MB_P");
            systemPowerSensorIndex = FindSensorIndex(sensors, "SYS_P");

            return cpuPowerSensorIndex >= 0
                && gpuPowerSensorIndex >= 0
                && mainboardPowerSensorIndex >= 0
                && systemPowerSensorIndex >= 0;
        }

        private static int FindSensorIndex(IList<Sensor> sensors, string shortName)
        {
            if (sensors == null)
            {
                return -1;
            }

            for (var index = 0; index < sensors.Count; index++)
            {
                var sensor = sensors[index];
                if (sensor != null
                    && string.Equals(sensor.ShortName, shortName, StringComparison.OrdinalIgnoreCase))
                {
                    return index;
                }
            }

            return -1;
        }

        private static string RemoveByteOrderMark(string value)
        {
            return value?.TrimStart('\uFEFF');
        }
    }

    internal sealed class BenchlabDeviceInfo
    {
        [JsonProperty("deviceName")]
        public string DeviceName { get; set; }

        [JsonProperty("productId")]
        public int ProductId { get; set; }

        [JsonProperty("guid")]
        public string DeviceId { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("pipeName")]
        public string PipeName { get; set; }

        [JsonIgnore]
        public bool IsConnected => string.Equals(Status, "CONNECTED", StringComparison.OrdinalIgnoreCase);
    }

    internal sealed class BenchlabSensorResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }

        [JsonProperty("sensorsUpdated")]
        public bool SensorsUpdated { get; set; }

        [JsonProperty("sensors")]
        public IList<Sensor> Sensors { get; set; }
    }
}
