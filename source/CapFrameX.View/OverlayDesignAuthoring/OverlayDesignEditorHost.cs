using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace CapFrameX.View.OverlayDesignAuthoring
{
    public readonly record struct OverlayDesignEditorState(bool IsOpen, string ProfileId, bool HasUnsavedChanges, bool IsBusy);

    /// <summary>The tile editor as automation sees it. All members are called on the UI thread.</summary>
    public interface IOverlayDesignEditorHost
    {
        OverlayDesignEditorState State { get; }

        /// <summary>Shows the library as another writer left it, positioned on <paramref name="profileId"/>.</summary>
        Task<bool> ReloadAsync(string profileId);

        Task OpenAsync(string profileId);
    }

    /// <summary>Follows the designer window, which the shell creates on demand.</summary>
    public sealed class OverlayDesignerWindowHost : IOverlayDesignEditorHost
    {
        private readonly Func<string, Task> _open;

        public OverlayDesignerWindowHost(Func<string, Task> open)
        {
            _open = open ?? throw new ArgumentNullException(nameof(open));
        }

        public OverlayDesignEditorState State
        {
            get
            {
                var window = Window;
                return window == null ? default
                    : new OverlayDesignEditorState(true, window.OpenProfileId, window.HasUnsavedDesignChanges, window.IsDesignerBusy);
            }
        }

        public Task<bool> ReloadAsync(string profileId) => Window?.ReloadLibraryAsync(profileId) ?? Task.FromResult(false);

        public Task OpenAsync(string profileId) => _open(profileId);

        private static OverlayDesignerWindow Window => Application.Current?.Windows.OfType<OverlayDesignerWindow>().FirstOrDefault();
    }
}
