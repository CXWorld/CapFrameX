using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Animation;

namespace CapFrameX
{
    /// <summary>
    /// Startup splash screen. Created and driven exclusively by
    /// <see cref="SplashScreenHost"/> on a dedicated dispatcher thread.
    /// </summary>
    public partial class SplashScreenWindow : Window
    {
        public SplashScreenWindow()
        {
            InitializeComponent();
            InitializeVersionInfo();
        }

        public void SetStatus(string status)
        {
            StatusText.Text = status;
        }

        /// <summary>
        /// Fades the window out and completes when the animation finished.
        /// </summary>
        public Task FadeOutAsync()
        {
            var completion = new TaskCompletionSource<bool>();

            var fadeOut = new DoubleAnimation(0d, TimeSpan.FromMilliseconds(180))
            {
                FillBehavior = FillBehavior.HoldEnd
            };
            fadeOut.Completed += (s, e) => completion.TrySetResult(true);
            BeginAnimation(OpacityProperty, fadeOut);

            return completion.Task;
        }

        private void InitializeVersionInfo()
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                VersionText.Text = $"v{assembly.GetName().Version}";

                // Same source as AppVersionProvider: Version.props stamps the channel
                // into the assembly metadata.
                var channel = assembly
                    .GetCustomAttributes<AssemblyMetadataAttribute>()
                    .FirstOrDefault(attribute => attribute.Key == "ReleaseChannel")
                    ?.Value;

                if (string.Equals(channel, "beta", StringComparison.OrdinalIgnoreCase))
                {
                    ChannelText.Text = "BETA";
                    ChannelBadge.Visibility = Visibility.Visible;
                }
            }
            catch
            {
                // Version info is decoration only - never let it break the splash.
            }
        }
    }
}
