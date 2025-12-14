using System.Collections.Generic;

namespace LibreHardwareMonitor.Hardware
{
    /// <summary>
    /// Caches GPU sensor information for efficient access.
    /// </summary>
    public sealed class GpuSensorCache
    {
        /// <summary>
        /// Gets the number of GPUs detected in the system.
        /// </summary>
        public int GpuCount { get; }
        /// <summary>
        /// Gets a mapping of sensor IDs to their corresponding GPU sensor information.
        /// </summary>
        public IReadOnlyDictionary<string, GpuSensorInfo> SensorsById { get; }
        /// <summary>
        /// Gets a mapping of GPU adapter names to sets of associated sensor IDs.
        /// </summary>
        public IReadOnlyDictionary<string, HashSet<string>> SensorIdsByAdapterName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GpuSensorCache"/> class.
        /// </summary>
        /// <param name="gpuCount"></param>
        /// <param name="sensorsById"></param>
        /// <param name="sensorIdsByAdapterName"></param>
        public GpuSensorCache(
            int gpuCount,
            Dictionary<string, GpuSensorInfo> sensorsById,
            Dictionary<string, HashSet<string>> sensorIdsByAdapterName)
        {
            GpuCount = gpuCount;
            SensorsById = sensorsById;
            SensorIdsByAdapterName = sensorIdsByAdapterName;
        }
    }

    /// <summary>
    /// Holds information about a GPU sensor.
    /// </summary>
    public readonly struct GpuSensorInfo
    {
        /// <summary>
        /// Gets the sensor instance.
        /// </summary>
        public ISensor Sensor { get; }
        /// <summary>
        /// Gets the name of the GPU adapter associated with the sensor.
        /// </summary>
        public string AdapterName { get; }
        /// <summary>
        /// Indicates whether the GPU is a discrete GPU.
        /// </summary>
        public bool IsDiscreteGpu { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="GpuSensorInfo"/> struct.
        /// </summary>
        /// <param name="sensor"></param>
        /// <param name="adapterName"></param>
        /// <param name="isDiscreteGpu"></param>
        public GpuSensorInfo(ISensor sensor, string adapterName, bool isDiscreteGpu)
        {
            Sensor = sensor;
            AdapterName = adapterName;
            IsDiscreteGpu = isDiscreteGpu;
        }
    }
}
