using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PcTool.Core.Settings;
using PcTool.Desktop.ViewModels;
using PcTool.Desktop.Views;

namespace PcTool.Desktop;

public partial class App : Application
{
    private ServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _services = ServiceSetup.Build();
            _services.GetRequiredService<ILogger<App>>().LogInformation("PcTool gestartet");

            var settingsStore = _services.GetRequiredService<ISettingsStore>();
            var settings = settingsStore.Load();

            var mainViewModel = _services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = mainViewModel };

            desktop.Exit += (_, _) =>
            {
                settingsStore.Save(settings);
                _services.Dispose();
            };

            // Läuft im Hintergrund weiter, das Fenster erscheint sofort.
            _ = mainViewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
