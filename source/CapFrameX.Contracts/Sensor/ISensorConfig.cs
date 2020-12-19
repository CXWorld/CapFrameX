using System.Threading.Tasks;

namespace CapFrameX.Contracts.Sensor
{
    public interface ISensorConfig
    {
        bool IsInitialized { get; set; }

        bool GlobalIsActivated{ get; set; }

        bool GetSensorIsActive(string identifier);

        void SetSensorIsActive(string identifier, bool isActive);

        Task Save();
    }
}
