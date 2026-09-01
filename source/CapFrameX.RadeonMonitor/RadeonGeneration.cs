namespace CapFrameX.RadeonMonitor
{
    internal enum RadeonGeneration
    {
        Rdna2 = 2,
        Rdna3 = 3,
        Rdna4 = 4
    }

    internal enum GenerationSelection
    {
        Auto,
        Rdna2,
        Rdna3,
        Rdna4
    }

    internal enum Rdna2MetricsLayout
    {
        Auto,
        Base,
        V2,
        V3,
        V4
    }

    internal enum Rdna3MetricsLayout
    {
        Auto,
        Smu13_0_0,
        Smu13_0_7
    }
}
