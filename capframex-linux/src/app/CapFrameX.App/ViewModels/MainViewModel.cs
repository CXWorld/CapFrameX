using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CapFrameX.Core.Capture;
using Microsoft.Extensions.DependencyInjection;

namespace CapFrameX.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly CaptureService _captureService;

    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _connectionStatus = "Disconnected";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private int _selectedTabIndex;

    public CaptureViewModel CaptureViewModel { get; }
    public AnalysisViewModel AnalysisViewModel { get; }
    public CompareViewModel CompareViewModel { get; }

    public MainViewModel(CaptureService captureService)
    {
        _captureService = captureService;

        CaptureViewModel = App.Services.GetRequiredService<CaptureViewModel>();
        AnalysisViewModel = App.Services.GetRequiredService<AnalysisViewModel>();
        CompareViewModel = App.Services.GetRequiredService<CompareViewModel>();

        CurrentView = CaptureViewModel;

        _captureService.ConnectionStatus.Subscribe(connected =>
        {
            IsConnected = connected;
            ConnectionStatus = connected ? "Connected" : "Disconnected";
        });

        // Auto-connect on startup
        _ = ConnectAsync();
    }

    [RelayCommand]
    private async Task ConnectAsync()
    {
        ConnectionStatus = "Connecting...";
        var success = await _captureService.ConnectAsync();
        if (!success)
        {
            ConnectionStatus = "Connection failed";
        }
    }

    [RelayCommand]
    private void SelectTab(string indexStr)
    {
        if (int.TryParse(indexStr, out var index))
            SelectedTabIndex = index;
    }

    partial void OnSelectedTabIndexChanged(int value)
    {
        CurrentView = value switch
        {
            0 => CaptureViewModel,
            1 => AnalysisViewModel,
            2 => CompareViewModel,
            _ => CaptureViewModel
        };
    }
}
