using Avalonia.Data.Converters;
using Avalonia.Media;
using PcTool.Core.Modules;
using PcTool.Desktop.Resources;

namespace PcTool.Desktop.Converters;

/// <summary>
/// Übersetzt einen Schweregrad in Ampelfarbe bzw. Text für die Anzeige.
/// </summary>
public static class SeverityConverters
{
    private static readonly IBrush OkBrush = new SolidColorBrush(Color.Parse("#2E9E44"));
    private static readonly IBrush InfoBrush = new SolidColorBrush(Color.Parse("#2F7FD1"));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.Parse("#E0A100"));
    private static readonly IBrush CriticalBrush = new SolidColorBrush(Color.Parse("#D13438"));

    public static readonly IValueConverter ToBrush = new FuncValueConverter<Severity?, IBrush>(severity => severity switch
    {
        Severity.Ok => OkBrush,
        Severity.Info => InfoBrush,
        Severity.Warning => WarningBrush,
        Severity.Critical => CriticalBrush,
        _ => Brushes.Gray,
    });

    public static readonly IValueConverter ToText = new FuncValueConverter<Severity?, string>(severity => severity switch
    {
        Severity.Ok => Strings.Severity_Ok,
        Severity.Info => Strings.Severity_Info,
        Severity.Warning => Strings.Severity_Warning,
        Severity.Critical => Strings.Severity_Critical,
        _ => string.Empty,
    });
}
