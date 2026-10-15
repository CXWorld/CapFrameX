using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using CapFrameX.Contracts.Localization;
using CapFrameX.OSD.Controls;
using CapFrameX.OSD.Integration;
using CapFrameX.OSD.Interop;
using CapFrameX.View.OverlayDesignAuthoring;
using Newtonsoft.Json;

namespace CapFrameX.View.Controls
{
    /// <summary>
    /// CapFrameX's telemetry and theme adapter around the reusable native OSD editor.
    /// The host owns the service; this control owns only its visible live-preview lease.
    /// It can later be embedded on OverlayView without changing the editor or sensor pipeline.
    /// </summary>
    public partial class OverlayDesignerControl : UserControl
    {
        public static readonly DependencyProperty TelemetryServiceProperty = DependencyProperty.Register(
            nameof(TelemetryService), typeof(IOverlayTelemetryService), typeof(OverlayDesignerControl),
            new PropertyMetadata(null, OnTelemetryServiceChanged));

        public static readonly DependencyProperty FrameFeedProperty = DependencyProperty.Register(
            nameof(FrameFeed), typeof(IObservable<OverlayPreviewFrame>), typeof(OverlayDesignerControl),
            new PropertyMetadata(null, OnFrameFeedChanged));

        private IDisposable _previewLease;
        private IDisposable _snapshotSubscription;
        private IDisposable _frameSubscription;
        private OverlayTelemetrySnapshot _pendingSnapshot;
        private readonly object _frameGate = new object();
        private readonly Queue<OsdStreamSample> _pendingFrames = new Queue<OsdStreamSample>();
        private bool _resetFrames;
        private readonly DispatcherTimer _previewTimer;
        private int _sourceGeneration;
        private int _liveGeneration;
        private Window _ownerWindow;
        private OverlayClassicPreviewFeed _classicPreview;
        private IReadOnlyList<OverlayTelemetrySource> _previewSources = Array.Empty<OverlayTelemetrySource>();
        private bool _classicReadPending;
        private Action _editClassicProfile;
        private Func<string> _sourceStatusText;
        private bool _languageSubscribed;

        public void ConfigureClassicPreview(OverlayClassicPreviewFeed feed, Action editProfile = null)
        {
            _classicPreview = feed;
            _editClassicProfile = editProfile;
            Editor.CanEditClassicProfile = editProfile != null;
        }

        public IOverlayTelemetryService TelemetryService
        {
            get => (IOverlayTelemetryService)GetValue(TelemetryServiceProperty);
            set => SetValue(TelemetryServiceProperty, value);
        }

        public OsdEditorControl Designer => Editor;

        public IObservable<OverlayPreviewFrame> FrameFeed
        {
            get => (IObservable<OverlayPreviewFrame>)GetValue(FrameFeedProperty);
            set => SetValue(FrameFeedProperty, value);
        }

        public OverlayDesignerControl()
        {
            OsdLocalization.SetLanguage(CxLang.Instance.UiLanguage);
            InitializeComponent();
            _previewTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _previewTimer.Tick += OnPreviewTick;
            Editor.EditClassicProfileRequested += (_, _) => _editClassicProfile?.Invoke();
            InitializeProfiles();
        }

        private static void OnTelemetryServiceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var control = (OverlayDesignerControl)sender;
            control.StopLivePreview();
            control._previewSources = Array.Empty<OverlayTelemetrySource>();
            ++control._sourceGeneration;
            if (control.IsLoaded)
            {
                control.RefreshSources();
                control.UpdateLivePreview();
            }
        }

        private static void OnFrameFeedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            var control = (OverlayDesignerControl)sender;
            control.StopLivePreview();
            control.UpdateLivePreview();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!_languageSubscribed)
            {
                CxLang.Instance.PropertyChanged += OnUiLanguageChanged;
                _languageSubscribed = true;
            }
            RefreshLanguage();
            AttachOwnerWindow();
            RefreshSources();
            UpdateLivePreview();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (_languageSubscribed)
            {
                CxLang.Instance.PropertyChanged -= OnUiLanguageChanged;
                _languageSubscribed = false;
            }
            ++_sourceGeneration;
            StopLivePreview();
            DetachOwnerWindow();
        }

        private void OnIsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateLivePreview();
        private void OnLivePreviewChanged(object sender, EventArgs e) => UpdateLivePreview();
        private void OnRefreshSources(object sender, RoutedEventArgs e) => RefreshSources();

        private void OnUiLanguageChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(CxLang.UiLanguage)) return;
            if (Dispatcher.CheckAccess()) RefreshLanguage();
            else if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                _ = Dispatcher.BeginInvoke(new Action(() => { if (_languageSubscribed) RefreshLanguage(); }));
        }

        private void RefreshLanguage()
        {
            OsdLocalization.SetLanguage(CxLang.Instance.UiLanguage);
            if (_sourceStatusText != null) SourceStatus.Text = _sourceStatusText();
            if (_profileErrorText != null) ProfileError.Text = _profileErrorText();
            if (_profileSession == null && _profileErrorText != null)
                ProfileStatus.Text = CxLang.T("OverlayEditor_ReadOnlyLibrary");
            UpdateProfileUi();
        }

        private void SetSourceStatus(Func<string> message)
        {
            _sourceStatusText = message;
            SourceStatus.Text = message();
        }

        private async void RefreshSources()
        {
            var service = TelemetryService;
            int generation = ++_sourceGeneration;
            if (service == null)
            {
                Editor.Sources = Array.Empty<OsdTelemetrySource>();
                SetSourceStatus(() => CxLang.T("OverlayEditor_DemoSources"));
                RefreshSourcesButton.IsEnabled = false;
                return;
            }

            RefreshSourcesButton.IsEnabled = false;
            SetSourceStatus(() => CxLang.T("OverlayEditor_DiscoveringSources"));
            try
            {
                var sources = await service.GetSourcesAsync();
                if (!IsLoaded || generation != _sourceGeneration) return;
                _previewSources = sources;
                Editor.Sources = OverlayDesignSources.ToEditorSources(sources);
                SetSourceStatus(() => CxLang.Format("OverlayEditor_SourcesReady", sources.Count));
            }
            catch (Exception ex)
            {
                if (IsLoaded && generation == _sourceGeneration)
                    SetSourceStatus(() => CxLang.Format("OverlayEditor_SourcesFailed", OverlayDesignText.ExceptionMessage(ex)));
            }
            finally
            {
                if (IsLoaded && generation == _sourceGeneration)
                {
                    RefreshSourcesButton.IsEnabled = true;
                    CompleteInitialProfileBinding();
                }
            }
        }

        private void UpdateLivePreview()
        {
            if (Editor == null || _previewTimer == null) return;
            bool live = IsLoaded && IsVisible && Editor.IsLivePreview && TelemetryService != null &&
                (_ownerWindow == null || (_ownerWindow.IsVisible && _ownerWindow.WindowState != WindowState.Minimized));
            if (!live)
            {
                StopLivePreview();
                return;
            }
            if (_previewLease != null) return;

            // Clear demo values before a real snapshot arrives, including when the hardware is unavailable.
            Editor.SetMetricsJson("{}");
            Editor.UpdateClassicEntries(Array.Empty<OsdEntry>());
            try
            {
                int generation;
                lock (_frameGate) generation = ++_liveGeneration;
                _snapshotSubscription = TelemetryService.Snapshots.Subscribe(
                    snapshot =>
                    {
                        lock (_frameGate)
                        {
                            if (generation == _liveGeneration)
                                Interlocked.Exchange(ref _pendingSnapshot, snapshot);
                        }
                    },
                    ex => Dispatcher.BeginInvoke(new Action(() =>
                    {
                        if (generation != _liveGeneration) return;
                        StopLivePreview();
                        Editor.SetMetricsJson("{}");
                        Editor.UpdateClassicEntries(Array.Empty<OsdEntry>());
                        SetSourceStatus(() => CxLang.Format("OverlayEditor_TelemetryStopped", OverlayDesignText.ExceptionMessage(ex)));
                    })));
                _previewLease = TelemetryService.AcquirePreview();
                _frameSubscription = FrameFeed?.Subscribe(frame => OnFrame(frame, generation),
                    _ => ClearPendingFrames(generation));
                Editor.ResetSamples();
                _previewTimer.Start();
            }
            catch (Exception ex)
            {
                StopLivePreview();
                SetSourceStatus(() => CxLang.Format("OverlayEditor_PreviewUnavailable", OverlayDesignText.ExceptionMessage(ex)));
            }
        }

        private void OnPreviewTick(object sender, EventArgs e)
        {
            var snapshot = Interlocked.Exchange(ref _pendingSnapshot, null);
            if (snapshot != null)
            {
                Editor.SetMetricsJson(JsonConvert.SerializeObject(snapshot.MetricValues));
                RefreshClassicPreview(snapshot);
            }
            OsdStreamSample[] samples;
            bool reset;
            lock (_frameGate)
            {
                reset = _resetFrames;
                _resetFrames = false;
                samples = _pendingFrames.ToArray();
                _pendingFrames.Clear();
            }
            if (reset) Editor.ResetSamples();
            if (samples.Length != 0) Editor.PushSamples(samples);
        }

        private async void RefreshClassicPreview(OverlayTelemetrySnapshot snapshot)
        {
            if (_classicReadPending || _classicPreview == null ||
                !Editor.Document.EnumerateTiles().Any(tile => tile.Kind == "classicRows")) return;
            _classicReadPending = true;
            int generation = _liveGeneration;
            int sourceGeneration = _sourceGeneration;
            var feed = _classicPreview;
            try
            {
                var entries = await feed.ReadAsync(snapshot, _previewSources);
                if (generation == _liveGeneration && sourceGeneration == _sourceGeneration &&
                    ReferenceEquals(feed, _classicPreview) && Editor.IsLivePreview)
                    Editor.UpdateClassicEntries(entries);
            }
            catch (Exception error)
            {
                if (generation != _liveGeneration) return;
                Editor.UpdateClassicEntries(Array.Empty<OsdEntry>());
                SetSourceStatus(() => CxLang.Format("OverlayEditor_RowsFailed", OverlayDesignText.ExceptionMessage(error)));
            }
            finally { _classicReadPending = false; }
        }

        private void OnFrame(OverlayPreviewFrame frame, int generation)
        {
            lock (_frameGate)
            {
                if (generation != _liveGeneration) return;
                if (frame.IsReset)
                {
                    _pendingFrames.Clear();
                    _resetFrames = true;
                    return;
                }
                // A stalled dispatcher must never grow an unbounded per-present queue.
                if (_pendingFrames.Count == 4096) _pendingFrames.Dequeue();
                _pendingFrames.Enqueue(new OsdStreamSample(frame.TimeMs, frame.FrametimeMs, frame.DisplayTimeMs));
            }
        }

        private void ClearPendingFrames(int? generation = null)
        {
            lock (_frameGate)
            {
                if (generation.HasValue && generation.Value != _liveGeneration) return;
                _pendingFrames.Clear();
                _resetFrames = true;
            }
        }

        private void StopLivePreview()
        {
            lock (_frameGate) ++_liveGeneration;
            _previewTimer?.Stop();
            _snapshotSubscription?.Dispose();
            _snapshotSubscription = null;
            _frameSubscription?.Dispose();
            _frameSubscription = null;
            _previewLease?.Dispose();
            _previewLease = null;
            Interlocked.Exchange(ref _pendingSnapshot, null);
            ClearPendingFrames();
        }

        private void AttachOwnerWindow()
        {
            var window = Window.GetWindow(this);
            if (ReferenceEquals(window, _ownerWindow)) return;
            DetachOwnerWindow();
            _ownerWindow = window;
            if (window == null) return;
            window.StateChanged += OnOwnerStateChanged;
            window.IsVisibleChanged += OnOwnerVisibilityChanged;
        }

        private void DetachOwnerWindow()
        {
            if (_ownerWindow == null) return;
            _ownerWindow.StateChanged -= OnOwnerStateChanged;
            _ownerWindow.IsVisibleChanged -= OnOwnerVisibilityChanged;
            _ownerWindow = null;
        }

        private void OnOwnerStateChanged(object sender, EventArgs e) => UpdateLivePreview();
        private void OnOwnerVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateLivePreview();
    }
}
