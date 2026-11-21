using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Omnikit.Core;
using Omnikit.Core.Elevation;
using Omnikit.Operations;
using Serilog;

// Omnikit.Helper: von der App mit Adminrechten gestartet. Aufruf: Omnikit.Helper.exe --elevated-operation <Aktion> <Pipe> [Argumente…]
// Führt genau eine Aktion aus der festen Liste aus (ElevatedOperations), meldet das Ergebnis über die Pipe und beendet sich.
// Liest keine Einstellungen des Benutzers.
if (!ElevatedOperationHost.IsHelperMode(args))
{
    return 2;
}

// Eigene Log-Datei, damit sich App und Hilfsprozess nicht gegenseitig die Datei sperren
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.File(
        Path.Combine(AppPaths.LogDirectory, "omnikit-helper-.log"),
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        formatProvider: System.Globalization.CultureInfo.InvariantCulture)
    .CreateLogger();

try
{
    var services = new ServiceCollection();
    services.AddLogging(logging => logging.AddSerilog());
    services.AddElevatedOperations();
    services.AddSingleton<IDestructiveConfirmation, Omnikit.Helper.MessageBoxConfirmation>();
    using var provider = services.BuildServiceProvider();

    var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("ElevatedHelper");
    return await ElevatedOperationHost.RunAsync(args, provider.GetServices<IElevatedOperation>(), logger);
}
#pragma warning disable CA1031 // Der Hilfsprozess soll nie mit einer Fehlermeldung abstürzen, sondern nur einen Fehlercode liefern.
catch (Exception ex)
#pragma warning restore CA1031
{
    Log.Fatal(ex, "Hilfsprozess fehlgeschlagen");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}
