using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace Omnikit.Core.Windows;

/// <summary>
/// Gemeinsame Helfer für WMI-Abfragen, die alle Module nutzen können.
/// </summary>
public static class Wmi
{
    /// <summary>Namespace der Windows-Storage-API (MSFT_PhysicalDisk, MSFT_StorageReliabilityCounter).</summary>
    public const string StorageNamespace = @"root\Microsoft\Windows\Storage";

    /// <summary>Fehler, die bei WMI-Abfragen erwartbar sind (z. B. Dienst nicht verfügbar, keine Rechte).</summary>
    public static bool IsExpectedError(Exception ex) =>
        ex is ManagementException or COMException or UnauthorizedAccessException;

    /// <summary>Führt eine Abfrage aus und kopiert die Werte jeder Zeile in ein Dictionary.</summary>
    [SupportedOSPlatform("windows")]
    public static List<Dictionary<string, object?>> Query(string scope, string query)
    {
        using var searcher = new ManagementObjectSearcher(scope, query);
        using var results = searcher.Get();
        var rows = new List<Dictionary<string, object?>>();

        foreach (var result in results)
        {
            using (result)
            {
                rows.Add(result.Properties.Cast<PropertyData>().ToDictionary(p => p.Name, p => (object?)p.Value));
            }
        }

        return rows;
    }

    [SupportedOSPlatform("windows")]
    public static List<Dictionary<string, object?>> Query(string query) => Query(@"root\cimv2", query);

    /// <summary>Wandelt einen WMI-Zahlenwert (UInt16, UInt64, ...) sicher in long um.</summary>
    public static long? ToLong(object? value) => value switch
    {
        null => null,
        IConvertible convertible => convertible.ToInt64(System.Globalization.CultureInfo.InvariantCulture),
        _ => null,
    };
}
