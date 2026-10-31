using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Omnikit.Modules.Health.Data;

namespace Omnikit.Modules.Health.Sources;

/// <summary>
/// Sucht über die Windows-Update-Schnittstelle (COM, "Microsoft.Update.Session") nach ausstehenden Updates.
/// Nur suchen, nichts installieren. Die Suche braucht Internet und kann dauern, deshalb gibt es ein Zeitlimit.
/// </summary>
public sealed class WindowsUpdateSource : IUpdateSource
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(60);

    private readonly ILogger<WindowsUpdateSource> _logger;

    public WindowsUpdateSource(ILogger<WindowsUpdateSource> logger)
    {
        _logger = logger;
    }

    public async Task<UpdateStatus?> CheckAsync(CancellationToken cancellationToken)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        // Die Suche selbst lässt sich nicht abbrechen. Nach dem Zeitlimit warten wir einfach nicht mehr auf sie.
        var search = Task.Run(Search, CancellationToken.None);
        var finished = await Task.WhenAny(search, Task.Delay(Timeout, cancellationToken));

        if (finished != search)
        {
            _logger.LogWarning("Update-Suche hat das Zeitlimit von {Seconds} s überschritten", Timeout.TotalSeconds);
            return null;
        }

        try
        {
            return await search;
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or Microsoft.CSharp.RuntimeBinder.RuntimeBinderException)
        {
            // z. B. 0x8024402C = keine Internetverbindung
            _logger.LogWarning(ex, "Update-Suche fehlgeschlagen");
            return null;
        }
    }

    [SupportedOSPlatform("windows")]
    private static UpdateStatus Search()
    {
        var sessionType = Type.GetTypeFromProgID("Microsoft.Update.Session")
            ?? throw new InvalidOperationException("Windows-Update-Schnittstelle nicht gefunden.");

        // "dynamic": Die COM-Schnittstelle wird zur Laufzeit angesprochen, ohne extra Bibliothek.
        dynamic session = Activator.CreateInstance(sessionType)!;
        dynamic searcher = session.CreateUpdateSearcher();
        dynamic result = searcher.Search("IsInstalled=0 and IsHidden=0 and Type='Software'");

        var total = 0;
        var security = 0;

        foreach (dynamic update in result.Updates)
        {
            total++;

            // MsrcSeverity ist nur bei Sicherheitsupdates gesetzt ("Critical", "Important", ...).
            string? severity = update.MsrcSeverity;
            if (!string.IsNullOrEmpty(severity))
            {
                security++;
            }
        }

        return new UpdateStatus(total, security);
    }
}
