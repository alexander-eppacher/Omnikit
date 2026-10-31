using Avalonia;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Omnikit.Core;
using Omnikit.Core.Elevation;
using Serilog;

namespace Omnikit.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static int Main(string[] args)
    {
        // Gestartet als Hilfsprozess mit Adminrechten? Dann KEINE Oberfläche, nur die eine Leseaktion.
        if (ElevatedOperationHost.IsHelperMode(args))
        {
            return RunElevatedHelper(args);
        }

        ConfigureLogging("omnikit-.log");
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Fatal(e.ExceptionObject as Exception, "Unbehandelter Fehler, die App wird beendet");

        try
        {
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            // Sorgt dafür, dass die letzten Log-Einträge noch in die Datei geschrieben werden.
            Log.CloseAndFlush();
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    /// <summary>
    /// Hilfsprozess: läuft unsichtbar mit Adminrechten, führt eine erlaubte Leseaktion aus und beendet sich.
    /// Eigene Log-Datei, damit sich die beiden Prozesse nicht gegenseitig die Datei sperren.
    /// </summary>
    private static int RunElevatedHelper(string[] args)
    {
        ConfigureLogging("omnikit-helper-.log");

        try
        {
            using var services = ServiceSetup.Build();
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("ElevatedHelper");
            var operations = services.GetServices<IElevatedOperation>();

            return ElevatedOperationHost.RunAsync(args, operations, logger).GetAwaiter().GetResult();
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
            Log.CloseAndFlush();
        }
    }

    /// <summary>
    /// Log-Datei pro Tag, die letzten 7 Tage bleiben erhalten.
    /// </summary>
    private static void ConfigureLogging(string fileName)
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(AppPaths.LogDirectory, fileName),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                formatProvider: System.Globalization.CultureInfo.InvariantCulture)
            .CreateLogger();
    }
}
