using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Omnikit.Core.Elevation;
using Omnikit.Operations;

namespace Omnikit.Core.Tests.Elevation;

public class ElevatedOperationsTests
{
    [Fact]
    public void OnlyTheseActionsRunWithAdminRights()
    {
        // Absichtlich ausgeschrieben: Eine neue Aktion mit Adminrechten soll niemand nebenbei hinzufügen.
        // Wer diese Liste ändert, arbeitet die Prüfliste „Neue Aktion im Admin-Hilfsprozess“ ab.
        var services = new ServiceCollection();
        services.AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance);
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddElevatedOperations();
        services.AddSingleton<IDestructiveConfirmation, ConfirmedInApp>();
        using var provider = services.BuildServiceProvider();

        var ids = provider.GetServices<IElevatedOperation>().Select(operation => operation.Id).Order(StringComparer.Ordinal);

        Assert.Equal(["app.install-update", "health.disk-reliability", "stick.create"], ids);
    }

    [Fact]
    public void HelperIsAlwaysTakenFromTheAppFolder()
    {
        var folder = Path.Combine(Path.GetTempPath(), "Omnikit");

        var path = ElevationRunner.HelperPathNextTo(folder + Path.DirectorySeparatorChar);

        // Absoluter Pfad im Ordner der App: nie über PATH oder das aktuelle Arbeitsverzeichnis gesucht
        Assert.True(Path.IsPathFullyQualified(path));
        Assert.Equal(Path.Combine(folder, ElevationRunner.HelperFileName), path);
    }
}
