namespace CapFrameX.RadeonMonitor
{
    internal static class RadeonDeviceClassifier
    {
        public static RadeonGeneration? DetectGeneration(ushort deviceId)
        {
            if (IsRdna4(deviceId))
            {
                return RadeonGeneration.Rdna4;
            }

            if (IsRdna3(deviceId))
            {
                return RadeonGeneration.Rdna3;
            }

            if (IsInRange(deviceId, 0x73A0, 0x73FF) ||
                IsInRange(deviceId, 0x7420, 0x743F))
            {
                return RadeonGeneration.Rdna2;
            }

            return null;
        }

        public static Rdna2MetricsLayout DetectRdna2Layout(ushort deviceId)
        {
            // Navi 21 defaults to V3; other RDNA2 devices default to V2.
            return IsInRange(deviceId, 0x73A0, 0x73BF)
                ? Rdna2MetricsLayout.V3
                : Rdna2MetricsLayout.V2;
        }

        public static Rdna3MetricsLayout DetectRdna3Layout(ushort deviceId)
        {
            // Navi 33 uses 13.0.7; Navi 31/32 share the 13.0.0 layout.
            return IsInRange(deviceId, 0x7480, 0x749F)
                ? Rdna3MetricsLayout.Smu13_0_7
                : Rdna3MetricsLayout.Smu13_0_0;
        }

        public static bool IsNavi21(ushort deviceId)
        {
            return IsInRange(deviceId, 0x73A0, 0x73BF);
        }

        public static bool IsRdna3(ushort deviceId)
        {
            return IsInRange(deviceId, 0x7440, 0x746F) ||
                IsInRange(deviceId, 0x7470, 0x749F);
        }

        public static bool IsRdna4(ushort deviceId)
        {
            return IsInRange(deviceId, 0x7550, 0x756F) ||
                IsInRange(deviceId, 0x7590, 0x75AF);
        }

        private static bool IsInRange(ushort value, ushort minimum, ushort maximum)
        {
            return value >= minimum && value <= maximum;
        }
    }
}
