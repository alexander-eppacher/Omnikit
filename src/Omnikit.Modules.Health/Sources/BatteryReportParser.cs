using System.Globalization;
using System.Xml.Linq;
using Omnikit.Modules.Health.Data;

namespace Omnikit.Modules.Health.Sources;

/// <summary>
/// Liest die Akkudaten aus dem XML-Bericht von "powercfg /batteryreport /xml".
/// Aufbau (vereinfacht):
///   &lt;BatteryReport&gt; &lt;Batteries&gt; &lt;Battery&gt;
///     &lt;Id&gt;…&lt;/Id&gt; &lt;DesignCapacity&gt;45000&lt;/DesignCapacity&gt;
///     &lt;FullChargeCapacity&gt;42090&lt;/FullChargeCapacity&gt; &lt;CycleCount&gt;179&lt;/CycleCount&gt;
///   &lt;/Battery&gt; &lt;/Batteries&gt; …
/// Die Seriennummer des Akkus steht auch drin, wird aber bewusst nicht gelesen.
/// </summary>
public static class BatteryReportParser
{
    public static IReadOnlyList<BatteryInfo> Parse(string xml)
    {
        var document = XDocument.Parse(xml);

        // Namespace-unabhängig suchen (LocalName), damit eine geänderte Schema-Version nicht alles kaputt macht.
        return document.Descendants()
            .Where(element => element.Name.LocalName == "Battery" && element.Parent?.Name.LocalName == "Batteries")
            .Select(battery => new BatteryInfo(
                ChildText(battery, "Id"),
                ParseLong(ChildText(battery, "DesignCapacity")),
                ParseLong(ChildText(battery, "FullChargeCapacity")),
                (int?)ParseLong(ChildText(battery, "CycleCount"))))
            .ToList();
    }

    private static string? ChildText(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(child => child.Name.LocalName == localName)?.Value.Trim();

    private static long? ParseLong(string? text) =>
        long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;
}
