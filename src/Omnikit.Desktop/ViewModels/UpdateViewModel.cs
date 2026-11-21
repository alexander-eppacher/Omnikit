using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Omnikit.Core;
using Omnikit.Core.Elevation;
using Omnikit.Core.Settings;
using Omnikit.Core.Updates;
using Omnikit.Desktop.Resources;
using Omnikit.Desktop.Services;
using Omnikit.Operations;

namespace Omnikit.Desktop.ViewModels;

/// <summary>
/// Hinweis auf eine neue Version (eine Anfrage bei jedem Start, abschaltbar; dazu „Jetzt suchen“) und Installation per Knopf.
/// Geladen wird ohne Adminrechte; die entscheidende Prüfung macht der Hilfsprozess im geschützten Ordner (0017).
/// </summary>
public partial class UpdateViewModel : ObservableObject
{
    private readonly ISettingsStore _store;
    private readonly IElevationRunner _elevation;
    private readonly IDialogService _dialogs;
    private readonly ILogger<UpdateViewModel> _logger;
    private readonly bool _loaded;

    public UpdateViewModel(ISettingsStore store, IElevationRunner elevation, IDialogService dialogs, ILogger<UpdateViewModel> logger)
    {
        _store = store;
        _elevation = elevation;
        _dialogs = dialogs;
        _logger = logger;
        CheckForUpdates = store.Load().CheckForUpdates;
        _loaded = true;
    }

    private static readonly string? BuildVersion =
        typeof(UpdateViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    /// <summary>Installierte Version (aus dem Build), z. B. 4.0.0.</summary>
    public static Version Installed { get; } = UpdateVerifier.InstalledVersion(BuildVersion);

    /// <summary>Für die Seite „Einstellungen“, z. B. „Version 4.0.1“ (ohne den Commit hinter dem „+“).</summary>
#pragma warning disable CA1822 // Die Oberfläche bindet an Eigenschaften des Objekts, nicht an statische
    public string VersionText => string.Format(CultureInfo.CurrentCulture, Strings.Update_Version, BuildVersion?.Split('+')[0] ?? Installed.ToString());
#pragma warning restore CA1822

    /// <summary>Läuft die installierte Version unter „Programme“? Nur sie aktualisiert sich selbst; portabel gibt es den Link.</summary>
    public static bool IsInstalled { get; } =
        Path.GetFullPath(AppContext.BaseDirectory).StartsWith(InstallUpdateOperation.InstallFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    public partial bool CheckForUpdates { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasOffer), nameof(OfferText))]
    public partial UpdateOffer? Offer { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(InstallCommand))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string? Status { get; set; }

    /// <summary>Ergebnis von „Jetzt suchen“, wenn es nichts Neues gibt (sonst erscheint der Hinweis oben).</summary>
    [ObservableProperty]
    public partial string? CheckResult { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckNowCommand))]
    public partial bool IsChecking { get; set; }

    public bool HasOffer => Offer is not null;

    public string? OfferText => Offer is null ? null : string.Format(CultureInfo.CurrentCulture, Strings.Update_Available, Offer.Manifest.Version);

#pragma warning disable CA1822 // Die Oberfläche bindet an Eigenschaften des Objekts, nicht an statische
    public string InstallButtonText => IsInstalled ? Strings.Update_Install : Strings.Update_OpenReleases;
#pragma warning restore CA1822

    partial void OnCheckForUpdatesChanged(bool value)
    {
        // Beim Start wird nur der gespeicherte Wert übernommen, dann gibt es nichts zu speichern
        if (_loaded)
        {
            Save(settings => settings with { CheckForUpdates = value });
        }
    }

    /// <summary>Beim Start: fragt, wenn eingeschaltet und Schlüssel eingebaut sind. So sieht man eine neue Version beim nächsten Öffnen.</summary>
    public async Task CheckOnStartAsync()
    {
        if (!_store.Load().CheckForUpdates || UpdateSource.TrustedKeys.Count == 0)
        {
            return;
        }

        await FindAsync();
    }

    /// <summary>Knopf „Jetzt suchen“: fragt sofort, auch wenn die tägliche Prüfung ausgeschaltet ist (der Nutzer will es ja gerade).</summary>
    [RelayCommand(CanExecute = nameof(CanCheckNow))]
    private async Task CheckNowAsync()
    {
        IsChecking = true;
        CheckResult = Strings.Update_Checking;
        try
        {
            await FindAsync();
            CheckResult = Offer is null ? Strings.Update_NoneFound : null;
        }
#pragma warning disable CA1031 // Ein Fehler bei der Suche soll als Meldung erscheinen, nicht die App beenden.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Update-Suche fehlgeschlagen");
            CheckResult = Strings.Update_CheckFailed;
        }
        finally
        {
            IsChecking = false;
        }
    }

    private bool CanCheckNow => !IsChecking && UpdateSource.TrustedKeys.Count > 0;

    // Liefert null bei „schon aktuell“ und bei Netzwerkfehlern; beides heißt für den Nutzer: nichts Neues gefunden
    private async Task FindAsync()
    {
        using var http = UpdateChecker.CreateHttpClient();
        Offer = await new UpdateChecker(http, UpdateSource.TrustedKeys).FindAsync(UpdateProduct.App, Installed, CancellationToken.None);
        _logger.LogInformation("Update-Prüfung: {Result}", Offer?.Manifest.Version ?? "keine neuere Version");
    }

    private bool CanInstall => !IsBusy && Offer is not null;

    [RelayCommand(CanExecute = nameof(CanInstall))]
    private async Task InstallAsync()
    {
        if (Offer is not { } offer)
        {
            return;
        }

        if (!IsInstalled)
        {
            await _dialogs.OpenWebPageAsync(new Uri(UpdateSource.Releases));
            return;
        }

        if (!await _dialogs.ConfirmAsync(Strings.Update_ConfirmTitle, string.Format(CultureInfo.CurrentCulture, Strings.Update_Confirm, offer.Manifest.Version)))
        {
            return;
        }

        IsBusy = true;
        try
        {
            Status = Strings.Update_Downloading;
            using var http = UpdateChecker.CreateHttpClient();
            var progress = new Progress<double>(part => Status = string.Format(CultureInfo.CurrentCulture, Strings.Update_DownloadingPercent, part * 100));
            var files = await new UpdateChecker(http, UpdateSource.TrustedKeys)
                .DownloadAsync(UpdateProduct.App, offer, Path.Combine(AppPaths.TempDirectory, "update"), progress, CancellationToken.None);

            Status = Strings.Update_Installing;
            var result = await _elevation.RunAsync(
                InstallUpdateOperation.OperationId, InstallUpdateOperation.Arguments(files.Manifest, files.Signature, files.Setup), null, CancellationToken.None);
            switch (result.Status)
            {
                case ElevationStatus.Success:
                    // Das Setup ersetzt jetzt die Dateien; dafür muss die App zu sein. Danach startet man Omnikit wie gewohnt neu.
                    await _dialogs.ConfirmAsync(Strings.Update_StartedTitle, Strings.Update_Started);
                    _dialogs.CloseApp();
                    break;
                case ElevationStatus.Cancelled:
                    Status = Strings.Update_Cancelled;
                    break;
                default:
                    Status = result.Payload ?? Strings.Update_Failed;
                    break;
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or TaskCanceledException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Update konnte nicht geladen werden");
            Status = Strings.Update_DownloadFailed;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Save(Func<AppSettings, AppSettings> change)
    {
        try
        {
            _store.Save(change(_store.Load()));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Einstellung zu Updates konnte nicht gespeichert werden");
        }
    }
}
