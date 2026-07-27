using CapFrameX.Contracts.Configuration;
using CapFrameX.Contracts.Data;
using CapFrameX.Contracts.Update;
using CapFrameX.MVVM.Dialogs;
using Microsoft.Extensions.Logging;
using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace CapFrameX.ViewModel
{
	/// <summary>
	/// Single source of update state for the whole shell. The status bar, the update tab in the
	/// options popup and the embedded dialog all bind against this instance, so it is registered as
	/// a singleton instead of being resolved per view.
	/// </summary>
	public class UpdateViewModel : BindableBase
	{
		private readonly IUpdateService _updateService;
		private readonly IAppConfiguration _appConfiguration;
		private readonly IAppVersionProvider _appVersionProvider;
		private readonly ILogger<UpdateViewModel> _logger;
		private readonly Dispatcher _dispatcher;

		private UpdateStatus _status = new UpdateStatus(EUpdateState.Unknown);
		private bool _isUpdateDialogOpen;

		public UpdateDialog UpdateDialogContent { get; }

		public bool IsUpdateDialogOpen
		{
			get { return _isUpdateDialogOpen; }
			set
			{
				_isUpdateDialogOpen = value;
				RaisePropertyChanged();
			}
		}

		public bool IsUpdateServerConfigured => _updateService.IsConfigured;

		public string InstalledVersionString => FormatVersion(_appVersionProvider.GetAppVersion());

		public string AvailableVersionString => Package == null ? "-" : FormatVersion(Package.Version);

		/// <summary>Full sentence for the tab and the status bar tooltip.</summary>
		public string StatusText
		{
			get
			{
				if (!IsUpdateServerConfigured)
					return "No update server is configured.";

				if (!string.IsNullOrWhiteSpace(_status.Message))
					return _status.Message;

				switch (_status.State)
				{
					case EUpdateState.Checking: return "Looking for updates...";
					case EUpdateState.UpToDate: return "CapFrameX is up to date.";
					case EUpdateState.Failed: return "The update check failed.";
					default: return "CapFrameX has not checked for updates yet.";
				}
			}
		}

		public bool IsChecking => _status.State == EUpdateState.Checking;

		public bool IsDownloading => _status.State == EUpdateState.Downloading;

		public bool IsUpdateAvailable => _status.State == EUpdateState.UpdateAvailable;

		public bool IsUpdateReadyToInstall => _status.State == EUpdateState.ReadyToInstall;

		/// <summary>Drives the download icon in the status bar.</summary>
		public bool IsUpdateIndicatorVisible => IsUpdateAvailable || IsDownloading || IsUpdateReadyToInstall;

		public double DownloadProgressPercent => _status.Progress * 100d;

		public string DownloadProgressText
			=> DownloadProgressPercent.ToString("F0", CultureInfo.InvariantCulture) + " %";

		public string ReleaseSummary => string.IsNullOrWhiteSpace(Package?.Summary)
			? "No description was provided for this update."
			: Package.Summary;

		public IReadOnlyList<string> ReleaseHighlights
			=> Package?.Highlights?.Where(highlight => !string.IsNullOrWhiteSpace(highlight)).ToArray()
				?? Array.Empty<string>();

		public bool HasReleaseHighlights => ReleaseHighlights.Count > 0;

		public string ReleaseDateText => Package?.ReleaseDate == null
			? string.Empty
			: Package.ReleaseDate.Value.ToString("d", CultureInfo.CurrentCulture);

		public bool HasReleaseDate => Package?.ReleaseDate != null;

		public string PackageSizeText
		{
			get
			{
				var sizeInBytes = Package?.SizeInBytes ?? 0L;

				return sizeInBytes <= 0L
					? string.Empty
					: $"Download size: {sizeInBytes / (1024d * 1024d):F1} MB";
			}
		}

		public bool HasReleaseNotesLink => Package?.ReleaseNotesUri != null;

		/// <summary>True while the dialog may offer to start the download.</summary>
		public bool CanConfirmUpdate => IsUpdateAvailable;

		public bool AutoUpdateCheckActive
		{
			get { return _appConfiguration.AutoUpdateCheckActive; }
			set
			{
				_appConfiguration.AutoUpdateCheckActive = value;
				RaisePropertyChanged();
			}
		}

		public ICommand CheckForUpdateCommand { get; }

		public ICommand ShowUpdateDialogCommand { get; }

		public ICommand ConfirmUpdateCommand { get; }

		public ICommand CancelDownloadCommand { get; }

		public ICommand OpenReleaseNotesCommand { get; }

		private UpdatePackageInfo Package => _status.Package;

		public UpdateViewModel(IUpdateService updateService,
			IAppConfiguration appConfiguration,
			IAppVersionProvider appVersionProvider,
			ILogger<UpdateViewModel> logger)
		{
			_updateService = updateService;
			_appConfiguration = appConfiguration;
			_appVersionProvider = appVersionProvider;
			_logger = logger;
			_dispatcher = Application.Current?.Dispatcher ?? Dispatcher.CurrentDispatcher;

			UpdateDialogContent = new UpdateDialog();

			var checkForUpdateCommand = new DelegateCommand(
				() => _ = CheckForUpdateAsync(), () => IsUpdateServerConfigured && !IsChecking && !IsDownloading);
			var showUpdateDialogCommand = new DelegateCommand(
				OnShowUpdateDialog, () => Package != null);
			var confirmUpdateCommand = new DelegateCommand(
				OnConfirmUpdate, () => CanConfirmUpdate);
			var cancelDownloadCommand = new DelegateCommand(
				() => _updateService.CancelDownload(), () => IsDownloading);

			CheckForUpdateCommand = checkForUpdateCommand;
			ShowUpdateDialogCommand = showUpdateDialogCommand;
			ConfirmUpdateCommand = confirmUpdateCommand;
			CancelDownloadCommand = cancelDownloadCommand;
			OpenReleaseNotesCommand = new DelegateCommand(OnOpenReleaseNotes, () => HasReleaseNotesLink);

			// BehaviorSubject replays the current status, so this also seeds the initial state.
			_updateService.StatusStream.Subscribe(status =>
			{
				Action apply = () => ApplyStatus(status);

				if (_dispatcher.CheckAccess()) apply();
				else _dispatcher.BeginInvoke(apply);
			});

			if (IsUpdateServerConfigured && AutoUpdateCheckActive)
			{
				// Deferred to idle so the check never competes with the rest of app start.
				_dispatcher.BeginInvoke(new Action(() => _ = CheckForUpdateAsync()),
					DispatcherPriority.ApplicationIdle);
			}
		}

		private async Task CheckForUpdateAsync()
		{
			try
			{
				await _updateService.CheckForUpdateAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// The service reports failures through its status stream; this only guards against
				// a command handler faulting the dispatcher.
				_logger.LogError(ex, "Error while checking for updates.");
			}
		}

		private void OnShowUpdateDialog()
		{
			if (Package == null)
				return;

			IsUpdateDialogOpen = true;
		}

		private void OnConfirmUpdate()
		{
			// Closing first lets the status bar and the update tab show the download progress.
			IsUpdateDialogOpen = false;

			_ = DownloadUpdateAsync();
		}

		private async Task DownloadUpdateAsync()
		{
			try
			{
				await _updateService.DownloadUpdateAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Error while downloading the update package.");
			}
		}

		private void OnOpenReleaseNotes()
		{
			var releaseNotesUri = Package?.ReleaseNotesUri;

			if (releaseNotesUri == null)
				return;

			try
			{
				// UseShellExecute is required to open a URL in the default browser on .NET Core+
				Process.Start(new ProcessStartInfo(releaseNotesUri.AbsoluteUri) { UseShellExecute = true });
			}
			catch (Exception ex)
			{
				_logger.LogError(ex, "Unable to open the release notes at {releaseNotesUri}.", releaseNotesUri);
			}
		}

		private void ApplyStatus(UpdateStatus status)
		{
			_status = status ?? new UpdateStatus(EUpdateState.Unknown);

			// A download that fails or is cancelled must not leave the dialog stranded on screen.
			if (IsUpdateDialogOpen && Package == null)
				IsUpdateDialogOpen = false;

			RaisePropertyChanged(nameof(StatusText));
			RaisePropertyChanged(nameof(IsChecking));
			RaisePropertyChanged(nameof(IsDownloading));
			RaisePropertyChanged(nameof(IsUpdateAvailable));
			RaisePropertyChanged(nameof(IsUpdateReadyToInstall));
			RaisePropertyChanged(nameof(IsUpdateIndicatorVisible));
			RaisePropertyChanged(nameof(DownloadProgressPercent));
			RaisePropertyChanged(nameof(DownloadProgressText));
			RaisePropertyChanged(nameof(AvailableVersionString));
			RaisePropertyChanged(nameof(ReleaseSummary));
			RaisePropertyChanged(nameof(ReleaseHighlights));
			RaisePropertyChanged(nameof(HasReleaseHighlights));
			RaisePropertyChanged(nameof(ReleaseDateText));
			RaisePropertyChanged(nameof(HasReleaseDate));
			RaisePropertyChanged(nameof(PackageSizeText));
			RaisePropertyChanged(nameof(HasReleaseNotesLink));
			RaisePropertyChanged(nameof(CanConfirmUpdate));

			(CheckForUpdateCommand as DelegateCommand)?.RaiseCanExecuteChanged();
			(ShowUpdateDialogCommand as DelegateCommand)?.RaiseCanExecuteChanged();
			(ConfirmUpdateCommand as DelegateCommand)?.RaiseCanExecuteChanged();
			(CancelDownloadCommand as DelegateCommand)?.RaiseCanExecuteChanged();
			(OpenReleaseNotesCommand as DelegateCommand)?.RaiseCanExecuteChanged();
		}

		/// <summary>
		/// CapFrameX releases on three components; the assembly's fourth one is build noise.
		/// </summary>
		private static string FormatVersion(Version version)
			=> version == null
				? "-"
				: $"{version.Major}.{version.Minor}.{Math.Max(version.Build, 0)}";
	}
}
