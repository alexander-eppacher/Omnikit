using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Omnikit.Core.Modules;

namespace Omnikit.Desktop.ViewModels;

/// <summary>Ein Programm im Katalog mit Häkchen.</summary>
public sealed partial class CatalogItemViewModel : ObservableObject
{
    public CatalogItemViewModel(string packageId, string name, string description)
    {
        PackageId = packageId;
        Name = name;
        Description = description;
    }

    public string PackageId { get; }

    public string Name { get; }

    public string Description { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChange))]
    public partial bool IsInstalled { get; set; }

    /// <summary>Installierte Version (nur Expertenansicht).</summary>
    [ObservableProperty]
    public partial string? InstalledVersion { get; set; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Installierte Programme kann man nicht noch einmal installieren; ihr Häkchen zählt nur für Vorlagen.</summary>
    public bool CanChange => !IsInstalled;
}

/// <summary>Ein Bereich im Katalog, z. B. „Browser“.</summary>
public sealed class CatalogGroupViewModel(string title, IEnumerable<CatalogItemViewModel> items)
{
    public string Title { get; } = title;

    public ObservableCollection<CatalogItemViewModel> Items { get; } = new(items);
}

/// <summary>Ein Programm mit verfügbarem Update.</summary>
public sealed partial class UpgradeItemViewModel(string packageId, string name, string versionText, string source) : ObservableObject
{
    public string PackageId { get; } = packageId;

    public string Name { get; } = name;

    /// <summary>z. B. „26.03 → 26.05“.</summary>
    public string VersionText { get; } = versionText;

    public string Source { get; } = source;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = true;
}

/// <summary>Ein Programm in der Fortschrittsliste während einer Installation oder Aktualisierung.</summary>
public sealed partial class JobViewModel(string packageId, string name, string stateText) : ObservableObject
{
    public string PackageId { get; } = packageId;

    public string Name { get; } = name;

    [ObservableProperty]
    public partial string StateText { get; set; } = stateText;

    /// <summary>Ampel: null = wartet/läuft, Ok = geschafft, Warning = nicht ausgeführt, Critical = fehlgeschlagen.</summary>
    [ObservableProperty]
    public partial Severity? Severity { get; set; }

    /// <summary>Fehlercode von winget (nur Expertenansicht).</summary>
    [ObservableProperty]
    public partial string? CodeText { get; set; }

    [ObservableProperty]
    public partial bool IsRunning { get; set; }
}
