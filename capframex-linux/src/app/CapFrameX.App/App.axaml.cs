using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CapFrameX.App.Views;
using CapFrameX.App.ViewModels;
using CapFrameX.Core.Capture;
using CapFrameX.Core.Configuration;
using CapFrameX.Core.Data;
using CapFrameX.Core.Hotkey;
using CapFrameX.Core.System;
using Microsoft.Extensions.DependencyInjection;

namespace CapFrameX.App;

public partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow
            {
                DataContext = Services.GetRequiredService<MainViewModel>()
            };

            desktop.ShutdownRequested += OnShutdownRequested;

            // Start global hotkey service
            var hotkeyService = Services.GetRequiredService<IGlobalHotkeyService>();
            hotkeyService.Start();
            Console.WriteLine("[App] Global hotkey service started");
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        // Stop global hotkey service
        var hotkeyService = Services.GetService<IGlobalHotkeyService>();
        hotkeyService?.Stop();
        hotkeyService?.Dispose();

        // Dispose services to stop daemon
        var captureService = Services.GetService<CaptureService>();
        captureService?.Dispose();

        var mainViewModel = Services.GetService<MainViewModel>();
        mainViewModel?.Dispose();

        var settingsService = Services.GetService<ISettingsService>();
        settingsService?.Dispose();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<CaptureService>();
        services.AddSingleton<FrametimeReceiver>();
        services.AddSingleton<SessionManager>();
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IGlobalHotkeyService, GlobalHotkeyService>();
        services.AddSingleton<ISystemInfoService, SystemInfoService>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<CaptureViewModel>();
        services.AddTransient<AnalysisViewModel>();
        services.AddTransient<CompareViewModel>();
        services.AddTransient<SettingsViewModel>();
    }
}
