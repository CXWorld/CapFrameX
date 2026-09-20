namespace CapFrameX.ViewModel
{
    public sealed class HookFreeDisplayItem
    {
        public HookFreeDisplayItem(string deviceName, string displayName, bool isPrimary)
        {
            DeviceName = deviceName;
            DisplayName = displayName;
            IsPrimary = isPrimary;
        }

        public string DeviceName { get; }

        public string DisplayName { get; }

        public bool IsPrimary { get; }
    }
}
