using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Omnikit.Desktop.ViewModels;
using Omnikit.Desktop.Views;

namespace Omnikit.Desktop;

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
            _services.GetRequiredService<ILogger<App>>().LogInformation("Omnikit gestartet");

            var mainViewModel = _services.GetRequiredService<MainViewModel>();
            desktop.MainWindow = new MainWindow { DataContext = mainViewModel };

            // Einstellungen werden dort gespeichert, wo sie sich ändern (z. B. ViewModeState), nicht hier beim Beenden:
            // Sonst würde der Stand vom Programmstart spätere Änderungen wieder überschreiben.
            desktop.Exit += (_, _) =>
            {
                mainViewModel.Shutdown();
                _services.Dispose();
            };

            // Läuft im Hintergrund weiter, das Fenster erscheint sofort.
            _ = mainViewModel.InitializeAsync();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
