using System.Text.Json;
using PcTool.Core.Elevation;
using PcTool.Modules.Health.Data;
using PcTool.Modules.Health.Sources;

namespace PcTool.Modules.Health;

/// <summary>
/// Die einzige Aktion, die der Hilfsprozess mit Adminrechten für das Health-Modul ausführt:
/// Laufwerksdetails lesen und als JSON zurückgeben. Nur lesen, nichts ändern.
/// </summary>
public sealed class DiskReliabilityOperation : IElevatedOperation
{
    public const string OperationId = "health.disk-reliability";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDiskReliabilitySource _source;

    public DiskReliabilityOperation(IDiskReliabilitySource source)
    {
        _source = source;
    }

    public string Id => OperationId;

    public Task<string> RunAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Serialize(_source.Read()));

    public static string Serialize(IReadOnlyList<DiskReliability> values) =>
        JsonSerializer.Serialize(values, JsonOptions);

    public static IReadOnlyList<DiskReliability>? Deserialize(string json) =>
        JsonSerializer.Deserialize<List<DiskReliability>>(json, JsonOptions);
}
