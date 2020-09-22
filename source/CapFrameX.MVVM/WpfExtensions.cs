using System.Windows;

namespace CapFrameX.MVVM
{
    public static class WpfExtensions
    {
        public static void ShowAndFocus(this Window W)
        {
            try
            {
                if (W.IsVisible && W.WindowState == WindowState.Minimized)
                {
                    W.WindowState = WindowState.Normal;
                }

                W.Show();
                W.Activate();
            }
            catch { }
        }
    }
}
