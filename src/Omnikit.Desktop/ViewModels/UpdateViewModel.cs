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
/// Hinweis auf eine neue Version (höchstens eine Anfrage am Tag, abschaltbar) und Installation per Knopf.
/// Geladen wird ohne Adminrechte; die entscheidende Prüfung macht der Hilfsprozess im geschützten Ordner (0017).
/// </summary>
public partial class UpdateViewModel : ObservableObject
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromDays(1);

    private readonly ISettingsStore _store;
    private readonly IElevationRunner _elevation;
    private readonly IDialogService _dialogs;
    private readonly TimeProvider _time;
    private readonly ILogger<UpdateViewModel> _logger;
    private readonly bool _loaded;

    public UpdateViewModel(ISettingsStore store, IElevationRunner elevation, IDialogService dialogs, TimeProvider time, ILogger<UpdateViewModel> logger)
    {
        _store = store;
        _elevation = elevation;
        _dialogs = dialogs;
        _time = time;
        _logger = logger;
        CheckForUpdates = store.Load().CheckForUpdates;
        _loaded = true;
    }

    /// <summary>Installierte Version (aus dem Build), z. B. 4.0.0.</summary>
    public static Version Installed { get; } = UpdateVerifier.InstalledVersion(
        typeof(UpdateViewModel).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

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

    /// <summary>Beim Start: fragt nur, wenn eingeschaltet, Schlüssel eingebaut sind und die letzte Anfrage über einen Tag her ist.</summary>
    public async Task CheckIfDueAsync()
    {
        var settings = _store.Load();
        var now = _time.GetUtcNow();
        if (!settings.CheckForUpdates || UpdateSource.TrustedKeys.Count == 0
            || (settings.LastUpdateCheck is { } last && now - last < CheckInterval && last <= now))
        {
            return;
        }

        Save(current => current with { LastUpdateCheck = now });
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
