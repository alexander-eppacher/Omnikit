using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace PcTool.Desktop.Controls;

/// <summary>
/// Verlaufsdiagramm wie im Task-Manager: neuester Wert rechts, die letzte Minute von rechts nach links.
/// Eigenes, kleines Steuerelement statt einer Diagramm-Bibliothek (keine neue Abhängigkeit nötig).
/// </summary>
public sealed class UsageGraph : Control
{
    /// <summary>Messwerte, ältester zuerst. Wird jede Sekunde durch eine neue Liste ersetzt.</summary>
    public static readonly StyledProperty<IReadOnlyList<double>?> ValuesProperty =
        AvaloniaProperty.Register<UsageGraph, IReadOnlyList<double>?>(nameof(Values));

    /// <summary>Oberer Rand des Diagramms, z. B. 100 für Prozent. 0 = automatisch nach dem größten Wert (für MB/s, Mbit/s).</summary>
    public static readonly StyledProperty<double> MaximumProperty =
        AvaloniaProperty.Register<UsageGraph, double>(nameof(Maximum), 100);

    /// <summary>Wie viele Werte in die Breite passen (60 = eine Minute).</summary>
    public static readonly StyledProperty<int> CapacityProperty =
        AvaloniaProperty.Register<UsageGraph, int>(nameof(Capacity), 60);

    public static readonly StyledProperty<Color> ColorProperty =
        AvaloniaProperty.Register<UsageGraph, Color>(nameof(Color), Color.Parse("#2F7FD1"));

    static UsageGraph()
    {
        // Bei jeder Änderung dieser Eigenschaften neu zeichnen.
        AffectsRender<UsageGraph>(ValuesProperty, MaximumProperty, CapacityProperty, ColorProperty);
    }

    public IReadOnlyList<double>? Values
    {
        get => GetValue(ValuesProperty);
        set => SetValue(ValuesProperty, value);
    }

    public double Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public int Capacity
    {
        get => GetValue(CapacityProperty);
        set => SetValue(CapacityProperty, value);
    }

    public Color Color
    {
        get => GetValue(ColorProperty);
        set => SetValue(ColorProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        var bounds = new Rect(Bounds.Size);
        if (bounds.Width < 2 || bounds.Height < 2)
        {
            return;
        }

        var line = new SolidColorBrush(Color);
        var fill = new SolidColorBrush(Color, 0.18);
        var grid = new Pen(new SolidColorBrush(Color, 0.15), 1);

        // Rahmen und Hilfslinien bei 25, 50 und 75 %
        context.DrawRectangle(new SolidColorBrush(Color, 0.04), new Pen(new SolidColorBrush(Color, 0.4), 1), bounds);
        for (var i = 1; i < 4; i++)
        {
            var y = bounds.Height * i / 4;
            context.DrawLine(grid, new Point(0, y), new Point(bounds.Width, y));
        }

        var values = Values;
        if (values is null || values.Count < 2)
        {
            return;
        }

        var maximum = Maximum > 0 ? Maximum : Math.Max(values.Max() * 1.1, 1);
        var step = bounds.Width / Math.Max(Capacity - 1, 1);

        // Punkt i: rechts der neueste Wert, nach links je eine Sekunde älter.
        Point ToPoint(int i) => new(
            bounds.Width - ((values.Count - 1 - i) * step),
            bounds.Height - (Math.Clamp(values[i] / maximum, 0, 1) * bounds.Height));

        var area = new StreamGeometry();
        using (var geometry = area.Open())
        {
            geometry.BeginFigure(new Point(ToPoint(0).X, bounds.Height), isFilled: true);
            for (var i = 0; i < values.Count; i++)
            {
                geometry.LineTo(ToPoint(i));
            }

            geometry.LineTo(new Point(bounds.Width, bounds.Height));
            geometry.EndFigure(isClosed: true);
        }

        var outline = new StreamGeometry();
        using (var geometry = outline.Open())
        {
            geometry.BeginFigure(ToPoint(0), isFilled: false);
            for (var i = 1; i < values.Count; i++)
            {
                geometry.LineTo(ToPoint(i));
            }

            geometry.EndFigure(isClosed: false);
        }

        context.DrawGeometry(fill, null, area);
        context.DrawGeometry(null, new Pen(line, 1.5), outline);
    }
}
