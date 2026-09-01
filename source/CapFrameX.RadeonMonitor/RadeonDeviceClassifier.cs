namespace CapFrameX.RadeonMonitor
{
    internal static class RadeonDeviceClassifier
    {
        public static RadeonGeneration? DetectGeneration(ushort deviceId)
        {
            if (IsInRange(deviceId, 0x7550, 0x756F) ||
                IsInRange(deviceId, 0x7590, 0x75AF))
            {
                return RadeonGeneration.Rdna4;
            }

            if (IsInRange(deviceId, 0x7440, 0x745F) ||
                IsInRange(deviceId, 0x7470, 0x749F))
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
            // Current Navi 21 firmware uses V3. Navi 22/23/24 use V2.
            // The UI keeps all four layouts selectable for old firmware.
            return IsInRange(deviceId, 0x73A0, 0x73BF)
                ? Rdna2MetricsLayout.V3
                : Rdna2MetricsLayout.V2;
        }

        public static Rdna3MetricsLayout DetectRdna3Layout(ushort deviceId)
        {
            // Navi 33 uses the SMU 13.0.7 interface. Navi 31 (13.0.0) and
            // Navi 32 (13.0.10) share the 13.0.0 metrics layout in amdgpu.
            return IsInRange(deviceId, 0x7480, 0x749F)
                ? Rdna3MetricsLayout.Smu13_0_7
                : Rdna3MetricsLayout.Smu13_0_0;
        }

        private static bool IsInRange(ushort value, ushort minimum, ushort maximum)
        {
            return value >= minimum && value <= maximum;
        }
    }
}
