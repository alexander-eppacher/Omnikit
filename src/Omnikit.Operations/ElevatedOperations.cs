using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Omnikit.Core.Elevation;
using Omnikit.Modules.Health;
using Omnikit.Modules.Health.Sources;
using Omnikit.Modules.StickBuilder.Creation;

namespace Omnikit.Operations;

/// <summary>
/// Alle Aktionen, die mit Adminrechten laufen dürfen. Was hier nicht steht, führt der Hilfsprozess nicht aus.
/// Eine neue Aktion braucht die Prüfliste „Neue Aktion im Admin-Hilfsprozess“ (eigene, typisierte Parameter, die der
/// Hilfsprozess selbst noch einmal prüft).
/// </summary>
public static class ElevatedOperations
{
    /// <summary>
    /// Registriert die Aktionen. Wer sie ausführt, registriert zusätzlich eine <see cref="IDestructiveConfirmation"/>:
    /// der Hilfsprozess ein eigenes Fenster, die App (wenn sie schon Admin ist) ihre eigene Bestätigung.
    /// </summary>
    public static IServiceCollection AddElevatedOperations(this IServiceCollection services)
    {
        // Liest nur: Fehlerzähler und Temperatur der Laufwerke (Windows gibt sie nur mit Adminrechten heraus)
        services.TryAddSingleton<WindowsDiskSource>();
        services.TryAddSingleton<IDiskReliabilitySource>(provider => provider.GetRequiredService<WindowsDiskSource>());
        services.AddSingleton<IElevatedOperation, DiskReliabilityOperation>();

        // Ändert: löscht einen USB-Stick und beschreibt ihn neu, nur nach Bestätigung
        services.AddSingleton<IElevatedOperation, CreateStickOperation>();

        // Ändert: installiert ein signiertes Update der App (nur die installierte Version), nur nach Bestätigung in der App
        services.AddSingleton<IElevatedOperation, InstallUpdateOperation>();
        return services;
    }
}
