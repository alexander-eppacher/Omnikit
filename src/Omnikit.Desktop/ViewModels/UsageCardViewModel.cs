using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Omnikit.Core.Modules;

namespace Omnikit.Desktop.ViewModels;

/// <summary>
/// Eine Karte auf der Seite „Leistung“ (z. B. Prozessor oder „Laufwerk C: (SSD)“): großer Wert, Verlauf, Expertenwerte.
/// Die Werte werden jede Sekunde aktualisiert, die Karte selbst bleibt bestehen.
/// </summary>
public partial class UsageCardViewModel : ObservableObject
{
    public UsageCardViewModel(string key, int order)
    {
        Key = key;
        Order = order;
    }

    /// <summary>Eindeutiger Schlüssel, z. B. "disk-0" oder die ID eines Netzwerkadapters.</summary>
    public string Key { get; }

    /// <summary>Position auf der Seite (CPU zuerst, dann RAM, Laufwerke, Grafik, Netzwerk).</summary>
    public int Order { get; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string? Subtitle { get; set; }

    /// <summary>Großer Wert, z. B. "23 %".</summary>
    [ObservableProperty]
    public partial string ValueText { get; set; } = string.Empty;

    /// <summary>Wort für Einsteiger, z. B. "wenig zu tun".</summary>
    [ObservableProperty]
    public partial string? Word { get; set; }

    /// <summary>Zweite Zeile, z. B. "Lesen 1,2 MB/s · Schreiben 0 B/s".</summary>
    [ObservableProperty]
    public partial string? DetailText { get; set; }

    /// <summary>Ampel (Durchschnitt über 30 Sekunden). <c>null</c> = wird nicht bewertet (Grafik, Netzwerk).</summary>
    [ObservableProperty]
    public partial Severity? Severity { get; set; }

    [ObservableProperty]
    public partial IReadOnlyList<double>? History { get; set; }

    /// <summary>100 für Prozent; 0 = Diagramm skaliert automatisch (Netzwerk).</summary>
    [ObservableProperty]
    public partial double GraphMaximum { get; set; } = 100;

    /// <summary>Wird die Karte in der aktuellen Ansicht gezeigt? (z. B. virtuelle Netzwerkadapter nur für Experten)</summary>
    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowExpert { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowCores))]
    public partial bool HasCores { get; set; }

    public bool ShowCores => ShowExpert && HasCores;

    public ObservableCollection<ExpertRow> ExpertRows { get; } = [];

    /// <summary>Verlauf je logischem Prozessor (nur Prozessor-Karte, nur Expertenansicht).</summary>
    public ObservableCollection<CoreGraph> Cores { get; } = [];

    partial void OnShowExpertChanged(bool value) => OnPropertyChanged(nameof(ShowCores));

    /// <summary>
    /// Aktualisiert die Expertenwerte. Bleiben die Bezeichnungen gleich, ändern sich nur die Werte –
    /// so bleibt eine Markierung (z. B. zum Kopieren einer IP-Adresse) erhalten.
    /// </summary>
    public void SetExpertRows(IReadOnlyList<(string Label, string Value)> rows)
    {
        if (ExpertRows.Count == rows.Count && ExpertRows.Select(row => row.Label).SequenceEqual(rows.Select(row => row.Label)))
        {
            for (var i = 0; i < rows.Count; i++)
            {
                ExpertRows[i].Value = rows[i].Value;
            }

            return;
        }

        ExpertRows.Clear();
        foreach (var (label, value) in rows)
        {
            ExpertRows.Add(new ExpertRow(label) { Value = value });
        }
    }

    public void SetCores(IReadOnlyList<double[]> histories)
    {
        while (Cores.Count > histories.Count)
        {
            Cores.RemoveAt(Cores.Count - 1);
        }

        for (var i = 0; i < histories.Count; i++)
        {
            if (i < Cores.Count)
            {
                Cores[i].Values = histories[i];
            }
            else
            {
                Cores.Add(new CoreGraph { Values = histories[i] });
            }
        }

        HasCores = histories.Count > 0;
    }
}

/// <summary>Eine Zeile der Expertenwerte, z. B. "Kerne: 8".</summary>
public partial class ExpertRow(string label) : ObservableObject
{
    public string Label { get; } = label;

    [ObservableProperty]
    public partial string Value { get; set; } = string.Empty;
}

/// <summary>Kleiner Verlauf eines logischen Prozessors.</summary>
public partial class CoreGraph : ObservableObject
{
    [ObservableProperty]
    public partial IReadOnlyList<double>? Values { get; set; }
}
