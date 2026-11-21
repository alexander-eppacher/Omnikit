using Omnikit.Core.Elevation;

namespace Omnikit.Agent;

/// <summary>
/// Der Agent läuft als SYSTEM und hat die Rechte schon; einen Hilfsprozess mit Windows-Abfrage gibt es im Dienst nicht.
/// Die Module fragen ihn deshalb nie an (sie lesen direkt), und falls doch, kommt „nicht möglich“ zurück.
/// </summary>
internal sealed class NoElevationRunner : IElevationRunner
{
    public Task<ElevationResult> RunAsync(string operationId, IReadOnlyList<string> arguments, IProgress<string>? progress, CancellationToken cancellationToken) =>
        Task.FromResult(new ElevationResult(ElevationStatus.NotSupported));
}
