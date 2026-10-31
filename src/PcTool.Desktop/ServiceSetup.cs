using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PcTool.Core;
using PcTool.Core.Elevation;
using PcTool.Core.Modules;
using PcTool.Core.Settings;
using PcTool.Core.SystemInfo;
using PcTool.Desktop.Services;
using PcTool.Desktop.ViewModels;
using PcTool.Modules.Health;
using PcTool.Modules.Health.Sources;
using PcTool.Modules.Performance.Sources;
using Serilog;

namespace PcTool.Desktop;

/// <summary>
/// Hier wird festgelegt, welche Klassen die App verwendet ("Dependency Injection").
/// Neue Module werden hier registriert, z. B. services.AddSingleton&lt;IModule, HealthModule&gt;().
/// </summary>
internal static class ServiceSetup
{
    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        // ILogger-Aufrufe landen bei Serilog (konfiguriert in Program.cs).
        services.AddLogging(logging => logging.AddSerilog());

        services.AddSingleton<ISettingsStore>(provider =>
            new JsonSettingsStore(AppPaths.SettingsFile, provider.GetRequiredService<ILogger<JsonSettingsStore>>()));
        services.AddSingleton(CreateSystemInfoProvider);
        services.AddSingleton<SystemContextBuilder>();

        services.AddSingleton(TimeProvider.System);

        // Hilfsprozess mit Adminrechten: Nur die hier registrierten Leseaktionen sind erlaubt (feste Liste).
        services.AddSingleton<IElevationRunner, ElevationRunner>();
        services.AddSingleton<IElevatedOperation, DiskReliabilityOperation>();

        // Health-Modul und seine Datenquellen
        services.AddSingleton<WindowsDiskSource>();
        services.AddSingleton<IDiskSource>(provider => provider.GetRequiredService<WindowsDiskSource>());
        services.AddSingleton<IDiskReliabilitySource>(provider => provider.GetRequiredService<WindowsDiskSource>());
        services.AddSingleton<IVolumeSource, VolumeSource>();
        services.AddSingleton<IBatterySource, PowercfgBatterySource>();
        services.AddSingleton<IWindowsStateSource, WindowsStateSource>();
        services.AddSingleton<IUpdateSource, WindowsUpdateSource>();

        // Seite "Leistung": Messquellen und die Seite selbst (braucht den SystemContext, der erst beim Start ermittelt wird)
        services.AddSingleton<IPerformanceSource, WindowsPerformanceSource>();
        services.AddSingleton<IHardwareInventorySource, WindowsHardwareInventorySource>();
        services.AddSingleton<Func<SystemContext, PerformancePageViewModel>>(provider => context => new PerformancePageViewModel(
            provider.GetRequiredService<IPerformanceSource>(),
            provider.GetRequiredService<IHardwareInventorySource>(),
            provider.GetRequiredService<ViewModeState>(),
            context,
            provider.GetRequiredService<ILogger<PerformancePageViewModel>>()));

        // Module: Reihenfolge = Reihenfolge in der Navigationsleiste
        services.AddSingleton<IModule, HealthModule>();
        services.AddSingleton<ModuleRegistry>();

        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<ViewModeState>();
        services.AddTransient<MainViewModel>();

        return services.BuildServiceProvider();
    }

    /// <summary>Unter Windows echte WMI-Werte, sonst "nicht verfügbar".</summary>
    private static ISystemInfoProvider CreateSystemInfoProvider(IServiceProvider provider)
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsSystemInfoProvider(provider.GetRequiredService<ILogger<WindowsSystemInfoProvider>>());
        }

        return new UnavailableSystemInfoProvider();
    }
}
