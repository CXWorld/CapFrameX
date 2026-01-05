using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using CapFrameX.App.Views;
using CapFrameX.App.ViewModels;
using CapFrameX.App.Services;
using CapFrameX.Core.Capture;
using CapFrameX.Core.Data;
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
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core services
        services.AddSingleton<CaptureService>();
        services.AddSingleton<FrametimeReceiver>();
        services.AddSingleton<SessionManager>();

        // ViewModels
        services.AddSingleton<MainViewModel>();
        services.AddTransient<CaptureViewModel>();
        services.AddTransient<AnalysisViewModel>();
        services.AddTransient<CompareViewModel>();
    }
}
