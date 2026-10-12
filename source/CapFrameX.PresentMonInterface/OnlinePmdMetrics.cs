namespace CapFrameX.PresentMonInterface
{
    public class OnlinePmdMetrics
    {
        public System.DateTime TimestampUtc { get; set; }

        public double GpuPowerCurrent { get; set; }

        public double CpuPowerCurrent { get; set; }

        public double SystemPowerCurrent { get; set; }
    }
}
