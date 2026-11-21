using Microsoft.Extensions.Logging;
using Omnikit.Core.Modules;
using Omnikit.Core.Reports;
using Omnikit.Core.SystemInfo;
using Omnikit.Modules.Apps.Winget;
using Omnikit.Shared;

namespace Omnikit.Agent;

/// <summary>
/// Erstellt den Bericht für den Server: dieselben Module wie in der App (Gerätezustand, Treiber, BIOS),
/// dasselbe Format wie der JSON-Export, dazu die Programmliste von winget. Liest nur.
/// </summary>
public sealed class ReportBuilder(
    SystemContextBuilder contextBuilder, IEnumerable<IModule> modules, IWingetClient winget, TimeProvider time, ILogger<ReportBuilder> logger)
{
    public async Task<ReportRequest> BuildAsync(CancellationToken cancellationToken)
    {
        var context = contextBuilder.Build();
        var reports = new List<ModuleReport>();
        foreach (var module in modules.Where(module => module.IsSupported(context)))
        {
            try
            {
                reports.Add(await module.CheckAsync(context, cancellationToken));
            }
#pragma warning disable CA1031 // Fällt ein Modul aus, sollen die anderen trotzdem gemeldet werden.
            catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
            {
                logger.LogError(ex, "Modul {Module} fehlgeschlagen", module.Id);
            }
        }

        return new ReportRequest(Environment.MachineName, ReportExporter.Create(context, reports, time.GetLocalNow()), await ReadInstalledAsync(cancellationToken));
    }

    private async Task<IReadOnlyList<InstalledProgram>?> ReadInstalledAsync(CancellationToken cancellationToken)
    {
        try
        {
            var installed = await winget.GetInstalledAsync(cancellationToken);
            return installed.Select(package => new InstalledProgram(package.Id, package.Version)).ToList();
        }
        catch (Exception ex) when (ex is WingetException or WingetMissingException)
        {
            // Ohne Programmliste meldet der Agent trotzdem; der Server zeigt „unbekannt“
            logger.LogWarning(ex, "Programmliste nicht lesbar");
            return null;
        }
    }
}
