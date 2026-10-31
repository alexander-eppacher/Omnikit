namespace Omnikit.Modules.Performance.Monitoring;

/// <summary>
/// Die letzten Messwerte eines Bauteils (Ringpuffer): Für den Verlauf im Diagramm und für den Durchschnitt.
/// Ist der Puffer voll, fällt der älteste Wert heraus.
/// </summary>
public sealed class LoadHistory
{
    /// <summary>Eine Minute bei einer Messung pro Sekunde.</summary>
    public const int DefaultCapacity = 60;

    private readonly double[] _values;
    private int _start;

    public LoadHistory(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _values = new double[capacity];
    }

    public int Capacity => _values.Length;

    public int Count { get; private set; }

    public void Add(double value)
    {
        if (Count < _values.Length)
        {
            _values[(_start + Count) % _values.Length] = value;
            Count++;
        }
        else
        {
            // Voll: den ältesten Wert überschreiben, der Anfang rückt eins weiter.
            _values[_start] = value;
            _start = (_start + 1) % _values.Length;
        }
    }

    /// <summary>Kopie der Werte, ältester zuerst. Eine Kopie, damit die Anzeige nicht mitten im Zeichnen eine Änderung sieht.</summary>
    public double[] ToArray()
    {
        var result = new double[Count];
        for (var i = 0; i < Count; i++)
        {
            result[i] = _values[(_start + i) % _values.Length];
        }

        return result;
    }

    /// <summary>Durchschnitt der neuesten <paramref name="count"/> Werte, oder <c>null</c>, solange es noch nicht so viele gibt.</summary>
    public double? AverageOfLast(int count)
    {
        if (count <= 0 || Count < count)
        {
            return null;
        }

        var sum = 0.0;
        for (var i = Count - count; i < Count; i++)
        {
            sum += _values[(_start + i) % _values.Length];
        }

        return sum / count;
    }

    public void Clear()
    {
        _start = 0;
        Count = 0;
    }
}
