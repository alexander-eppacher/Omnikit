using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Omnikit.Core.Elevation;
using Omnikit.Core.SystemInfo;
using Omnikit.Desktop.Services;
using Omnikit.Modules.StickBuilder;
using Omnikit.Modules.StickBuilder.Creation;
using Omnikit.Modules.StickBuilder.Download;
using Texts = Omnikit.Modules.StickBuilder.Resources.StickTexts;

namespace Omnikit.Desktop.ViewModels;

/// <summary>Ein Eintrag mit Häkchen, z. B. eine vorinstallierte App, die entfernt werden soll.</summary>
public sealed partial class ChoiceToggleViewModel(Choice choice, bool isSelected) : ObservableObject
{
    public Choice Choice { get; } = choice;

    [ObservableProperty]
    public partial bool IsSelected { get; set; } = isSelected;
}

/// <summary>
/// Seite „Windows-Stick“: stellt eine automatische Windows-Installation zusammen und speichert sie als ZIP
/// oder schreibt die Antwortdatei direkt auf einen Windows-Installationsstick (nur nach Bestätigung).
/// Die Eingaben werden laufend geprüft; Fehler stehen unten über den Knöpfen.
/// </summary>
public sealed partial class StickBuilderPageViewModel : ViewModelBase, IActivatablePage
{
    private readonly IDialogService _dialogs;
    private readonly IElevationRunner _elevation;
    private readonly WindowsDownloader _downloader;
    private readonly TimeProvider _time;
    private readonly ILogger _logger;

    // Erst prüfen, wenn alle Felder gesetzt sind (die Vorschläge im Konstruktor lösen schon Änderungen aus)
    private readonly bool _ready;

    public StickBuilderPageViewModel(
        IDialogService dialogs, IElevationRunner elevation, WindowsDownloader downloader, ViewModeState viewMode, SystemContext context,
        TimeProvider time, ILogger logger)
    {
        _dialogs = dialogs;
        _elevation = elevation;
        _downloader = downloader;
        _time = time;
        _logger = logger;
        ViewMode = viewMode;
        CanWriteToStick = context.IsWindows;
        Catalog = new CatalogPickerViewModel(dialogs, viewMode, logger);

        // Vorschläge passend zu diesem PC: Sprache, Format, Tastatur, Zeitzone und Edition
        SelectedLanguage = Pick(Choices.Languages, CultureInfo.CurrentUICulture.Name, "de-DE");
        SelectedFormat = Pick(Choices.Formats, CultureInfo.CurrentCulture.Name, SelectedLanguage.Value);
        SelectedKeyboard = Pick(Choices.Keyboards, KeyboardFor(SelectedFormat.Value), "0407:00000407");
        SelectedTimeZone = Pick(Choices.TimeZones, TimeZoneInfo.Local.Id, "W. Europe Standard Time");
        SelectedEdition = Pick(Choices.Editions, EditionOf(context.OsName).ToString(), nameof(WindowsEdition.Pro));

        RemovableApps = Choices.RemovableApps
            .Select(app => new ChoiceToggleViewModel(app, Choices.DefaultRemovedApps.Contains(app.Value)))
            .ToList();

        foreach (var toggle in RemovableApps)
        {
            toggle.PropertyChanged += (_, _) => Validate();
        }

        Catalog.SelectionChanged += (_, _) => Validate();
        _ready = true;
        Validate();
    }

    /// <summary>Für die Expertenansicht (Vorschau der Antwortdatei).</summary>
    public ViewModeState ViewMode { get; }

    public CatalogPickerViewModel Catalog { get; }

    public IReadOnlyList<Choice> Languages { get; } = Choices.Languages;

    public IReadOnlyList<Choice> Formats { get; } = Choices.Formats;

    public IReadOnlyList<Choice> Keyboards { get; } = Choices.Keyboards;

    public IReadOnlyList<Choice> TimeZones { get; } = Choices.TimeZones;

    public IReadOnlyList<Choice> Editions { get; } = Choices.Editions;

    public IReadOnlyList<ChoiceToggleViewModel> RemovableApps { get; }

    /// <summary>Sticks lassen sich nur unter Windows erkennen und beschreiben.</summary>
    public bool CanWriteToStick { get; }

    public ObservableCollection<WindowsStick> Sticks { get; } = [];

    /// <summary>USB-Laufwerke, aus denen ein Stick erstellt werden kann (werden dabei gelöscht).</summary>
    public ObservableCollection<UsbDisk> UsbDisks { get; } = [];

    /// <summary>Quelle: Windows 11 von Microsoft laden (statt eigener ISO-Datei).</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateStickCommand))]
    public partial bool UseDownload { get; set; }

    /// <summary>Windows-Dateien je Sprache aus dem Katalog von Microsoft.</summary>
    public ObservableCollection<WindowsDownload> Downloads { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateStickCommand))]
    public partial WindowsDownload? SelectedDownload { get; set; }

    /// <summary>„Liste wird geladen …“ oder ein Fehler beim Laden des Katalogs.</summary>
    [ObservableProperty]
    public partial string? CatalogStatus { get; set; }

    /// <summary>Pfad der gewählten ISO-Datei.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsoName))]
    [NotifyCanExecuteChangedFor(nameof(CreateStickCommand))]
    public partial string? IsoPath { get; set; }

    public string IsoName => IsoPath is null ? Texts.Label_NoIso : Path.GetFileName(IsoPath);

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateStickCommand))]
    public partial UsbDisk? SelectedUsbDisk { get; set; }

    /// <summary>Auch USB-Laufwerke zeigen, die sich als Festplatte melden. Wird nicht gespeichert: jedes Mal bewusst anhaken.</summary>
    [ObservableProperty]
    public partial bool IncludeFixedDisks { get; set; }

    partial void OnIncludeFixedDisksChanged(bool value) => RefreshUsbDisks();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateStickCommand))]
    public partial bool IsCreating { get; set; }

    /// <summary>Fortschritt beim Erstellen, z. B. „Windows wird auf den Stick kopiert … 42 %“.</summary>
    [ObservableProperty]
    public partial string? CreateProgress { get; set; }

    [ObservableProperty]
    public partial string? CreateStatus { get; set; }

    public bool HasUsbDisks => UsbDisks.Count > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LanguageWarning))]
    public partial Choice SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial Choice SelectedFormat { get; set; }

    [ObservableProperty]
    public partial Choice SelectedKeyboard { get; set; }

    [ObservableProperty]
    public partial Choice SelectedTimeZone { get; set; }

    [ObservableProperty]
    public partial Choice SelectedEdition { get; set; }

    [ObservableProperty]
    public partial string ComputerName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string AccountName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Password { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PasswordRepeat { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool WipeDisk { get; set; }

    /// <summary>Zweites Häkchen: „Ich weiß, dass alle Daten auf Datenträger 0 gelöscht werden.“</summary>
    [ObservableProperty]
    public partial bool WipeConfirmed { get; set; }

    [ObservableProperty]
    public partial string WifiName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WifiPassword { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool PrivacyDefaults { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowFileExtensions { get; set; } = true;

    [ObservableProperty]
    public partial bool TaskbarLeft { get; set; }

    [ObservableProperty]
    public partial bool HideWidgets { get; set; }

    [ObservableProperty]
    public partial bool ClassicContextMenu { get; set; }

    [ObservableProperty]
    public partial bool NoWebSearch { get; set; }

    [ObservableProperty]
    public partial bool PasswordNeverExpires { get; set; } = true;

    [ObservableProperty]
    public partial bool DisableFastStartup { get; set; }

    [ObservableProperty]
    public partial bool PreventDeviceEncryption { get; set; }

    [ObservableProperty]
    public partial bool BypassRequirements { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LanguageWarning))]
    [NotifyCanExecuteChangedFor(nameof(WriteToStickCommand), nameof(ImportAnswerFileCommand))]
    public partial WindowsStick? SelectedStick { get; set; }

    /// <summary>Was noch fehlt oder falsch ist; null = alles in Ordnung.</summary>
    [ObservableProperty]
    public partial string? ErrorText { get; set; }

    [ObservableProperty]
    public partial string? StatusMessage { get; set; }

    /// <summary>Antwortdatei zum Nachlesen (Expertenansicht).</summary>
    [ObservableProperty]
    public partial string? Preview { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveZipCommand), nameof(WriteToStickCommand))]
    public partial bool IsValid { get; set; }

    public bool HasSticks => Sticks.Count > 0;

    /// <summary>Hinweis, wenn die gewählte Sprache nicht auf dem Stick ist (Setup würde sonst scheitern).</summary>
    public string? LanguageWarning =>
        SelectedStick is { Languages.Count: > 0 } stick
        && !stick.Languages.Contains(SelectedLanguage.Value, StringComparer.OrdinalIgnoreCase)
            ? string.Format(CultureInfo.CurrentCulture, Texts.Page_LanguageMismatch, string.Join(", ", stick.Languages))
            : null;

    public void Activate()
    {
        if (CanWriteToStick && !IsCreating)
        {
            RefreshSticks();
            RefreshUsbDisks();
        }
    }

    public void Deactivate()
    {
    }

    // Jede Eingabe wird sofort geprüft
    partial void OnSelectedLanguageChanged(Choice value) => Validate();

    partial void OnSelectedFormatChanged(Choice value) => Validate();

    partial void OnSelectedKeyboardChanged(Choice value) => Validate();

    partial void OnSelectedTimeZoneChanged(Choice value) => Validate();

    partial void OnSelectedEditionChanged(Choice value) => Validate();

    partial void OnComputerNameChanged(string value) => Validate();

    partial void OnAccountNameChanged(string value) => Validate();

    partial void OnPasswordChanged(string value) => Validate();

    partial void OnPasswordRepeatChanged(string value) => Validate();

    partial void OnWipeDiskChanged(bool value)
    {
        WipeConfirmed = false;
        Validate();
    }

    partial void OnWipeConfirmedChanged(bool value) => Validate();

    partial void OnWifiNameChanged(string value) => Validate();

    partial void OnWifiPasswordChanged(string value) => Validate();

    partial void OnPrivacyDefaultsChanged(bool value) => Validate();

    partial void OnShowFileExtensionsChanged(bool value) => Validate();

    partial void OnTaskbarLeftChanged(bool value) => Validate();

    partial void OnHideWidgetsChanged(bool value) => Validate();

    partial void OnClassicContextMenuChanged(bool value) => Validate();

    partial void OnNoWebSearchChanged(bool value) => Validate();

    partial void OnPasswordNeverExpiresChanged(bool value) => Validate();

    partial void OnDisableFastStartupChanged(bool value) => Validate();

    partial void OnPreventDeviceEncryptionChanged(bool value) => Validate();

    partial void OnBypassRequirementsChanged(bool value) => Validate();

    [RelayCommand]
    private void RefreshSticks()
    {
        Sticks.Clear();
        try
        {
            foreach (var stick in WindowsStickFinder.Find())
            {
                Sticks.Add(stick);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "USB-Sticks konnten nicht gelesen werden");
        }

        SelectedStick = Sticks.FirstOrDefault();
        OnPropertyChanged(nameof(HasSticks));
    }

    [RelayCommand]
    private void RefreshUsbDisks()
    {
        UsbDisks.Clear();
        if (OperatingSystem.IsWindows())
        {
            try
            {
                foreach (var disk in UsbDiskFinder.List(IncludeFixedDisks))
                {
                    UsbDisks.Add(disk);
                }
            }
            catch (Exception ex) when (Omnikit.Core.Windows.Wmi.IsExpectedError(ex))
            {
                _logger.LogWarning(ex, "USB-Laufwerke konnten nicht gelesen werden");
            }
        }

        SelectedUsbDisk = UsbDisks.FirstOrDefault();
        OnPropertyChanged(nameof(HasUsbDisks));
    }

    [RelayCommand]
    private async Task PickIsoAsync()
    {
        if (await _dialogs.PickOpenFilePathAsync("ISO", "iso") is { } path)
        {
            IsoPath = path;
        }
    }

    private bool CanCreate => SelectedUsbDisk is not null && !IsCreating && (UseDownload ? SelectedDownload is not null : IsoPath is not null);

    partial void OnUseDownloadChanged(bool value)
    {
        if (value && Downloads.Count == 0)
        {
            LoadCatalogCommand.Execute(null);
        }
    }

    /// <summary>Katalog von Microsoft laden; die Sprache wird passend zur oben gewählten Windows-Sprache vorgeschlagen.</summary>
    [RelayCommand]
    private async Task LoadCatalogAsync()
    {
        CatalogStatus = Texts.Page_CatalogLoading;
        try
        {
            Downloads.Clear();
            foreach (var download in await _downloader.LoadCatalogAsync(CancellationToken.None))
            {
                Downloads.Add(download);
            }

            SelectedDownload = Downloads.FirstOrDefault(d => d.LanguageCode.Equals(SelectedLanguage.Value, StringComparison.OrdinalIgnoreCase))
                ?? Downloads.FirstOrDefault();
            CatalogStatus = null;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or System.Xml.XmlException or System.ComponentModel.Win32Exception)
        {
            _logger.LogWarning(ex, "Windows-Katalog konnte nicht geladen werden");
            CatalogStatus = Texts.Page_CatalogFailed;
        }
    }

    /// <summary>
    /// Erstellt den Stick im Hilfsprozess mit Adminrechten – nur nach Bestätigung, weil der Stick gelöscht wird (Regel 6.1.1).
    /// Danach die Frage, ob eine automatische Installation eingerichtet werden soll.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanCreate))]
    private async Task CreateStickAsync()
    {
        if (SelectedUsbDisk is not { } disk || (UseDownload ? SelectedDownload is null : IsoPath is null))
        {
            return;
        }

        var message = UseDownload
            ? string.Format(CultureInfo.CurrentCulture, Texts.Confirm_CreateDownload, disk.DisplayName, SelectedDownload!.Language)
            : string.Format(CultureInfo.CurrentCulture, Texts.Confirm_Create, disk.DisplayName, Path.GetFileName(IsoPath));
        if (!disk.IsRemovable)
        {
            message = Texts.Confirm_FixedWarning + Environment.NewLine + Environment.NewLine + message;
        }
        if (!await _dialogs.ConfirmAsync(Texts.Confirm_CreateTitle, message))
        {
            return;
        }

        IsCreating = true;
        CreateStatus = null;
        ElevationResult result;
        try
        {
            // Erst laden (ohne Adminrechte), dann mit Adminrechten auf den Stick schreiben
            IReadOnlyList<string> arguments;
            if (UseDownload)
            {
                var download = SelectedDownload!;
                var downloadProgress = new Progress<DownloadProgress>(p => CreateProgress = p.IsVerifying
                    ? Texts.Create_Verifying
                    : string.Format(CultureInfo.CurrentCulture, Texts.Create_Downloading, p.Percent, p.BytesDone / 1e9, p.BytesTotal / 1e9));
                string esd;
                try
                {
                    esd = await _downloader.DownloadAsync(download, downloadProgress, CancellationToken.None);
                }
                catch (Exception ex) when (ex is HttpRequestException or IOException)
                {
                    _logger.LogError(ex, "Windows-Download fehlgeschlagen");
                    CreateStatus = ex is InvalidDataException ? Texts.Create_HashMismatch : Texts.Page_DownloadFailed;
                    return;
                }

                arguments = CreateStickOperation.Arguments(disk, IncludeFixedDisks, esd, download.Sha1);
            }
            else
            {
                arguments = CreateStickOperation.Arguments(disk, IncludeFixedDisks, IsoPath!);
            }

            // Progress<T> meldet im UI-Thread zurück, weil es hier erzeugt wird
            CreateProgress = Texts.Create_Formatting;
            var progress = new Progress<string>(text => CreateProgress = text);
            result = await _elevation.RunAsync(CreateStickOperation.OperationId, arguments, progress, CancellationToken.None);
        }
        finally
        {
            IsCreating = false;
            CreateProgress = null;
        }

        _logger.LogInformation("Stick erstellen: {Status}", result.Status);
        switch (result.Status)
        {
            case ElevationStatus.Success when result.Payload?.Contains("\"windows\"", StringComparison.Ordinal) == true:
                CreateStatus = Texts.Page_CreateDoneWindows;
                RefreshSticks();
                if (await _dialogs.ConfirmAsync(Texts.Confirm_SetupTitle, Texts.Confirm_Setup))
                {
                    StatusMessage = Texts.Page_SetupHint;
                    CreateStatus = Texts.Page_CreateDoneWindows + " " + Texts.Page_SetupHint;
                }

                break;
            case ElevationStatus.Success:
                CreateStatus = Texts.Page_CreateDoneRaw;
                break;
            case ElevationStatus.Cancelled:
                CreateStatus = Texts.Page_CreateCancelled;
                break;
            default:
                // Erwartete Ablehnung (z. B. „kein USB-Stick“) kommt mit verständlicher Meldung zurück
                CreateStatus = result.Payload ?? Texts.Page_CreateFailed;
                break;
        }

        RefreshUsbDisks();
    }

    /// <summary>Eine vorhandene autounattend.xml (z. B. von schneegans.de) prüfen und nach Bestätigung auf den Stick legen.</summary>
    [RelayCommand(CanExecute = nameof(HasSelectedStick))]
    private async Task ImportAnswerFileAsync()
    {
        if (SelectedStick is not { } stick || await _dialogs.PickOpenFilePathAsync("XML", "xml") is not { } path)
        {
            return;
        }

        try
        {
            var content = await File.ReadAllTextAsync(path);
            if (!WindowsStickFinder.IsAnswerFile(content))
            {
                StatusMessage = Texts.Page_ImportInvalid;
                return;
            }

            var message = string.Format(CultureInfo.CurrentCulture, Texts.Confirm_Import, Path.GetFileName(path), stick.DisplayName);
            if (!await _dialogs.ConfirmAsync(Texts.Confirm_ImportTitle, message))
            {
                return;
            }

            await WindowsStickFinder.ImportAnswerFileAsync(stick, content, CancellationToken.None);
            _logger.LogInformation("Vorhandene Antwortdatei auf Stick {Root} kopiert", stick.Root);
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Texts.Page_StickWritten, stick.DisplayName);
            RefreshSticks();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Antwortdatei konnte nicht importiert werden");
            StatusMessage = Texts.Page_SaveFailed;
        }
    }

    private bool HasSelectedStick => SelectedStick is not null;

    [RelayCommand(CanExecute = nameof(IsValid))]
    private async Task SaveZipAsync()
    {
        try
        {
            await using var stream = await _dialogs.PickSaveFileAsync(StickPackage.SuggestFileName(_time.GetLocalNow()), "ZIP", "zip");
            if (stream is null)
            {
                return;
            }

            await StickPackage.WriteZipAsync(stream, BuildOptions(), CancellationToken.None);
            StatusMessage = Texts.Page_ZipSaved;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "ZIP für den Stick konnte nicht gespeichert werden");
            StatusMessage = Texts.Page_SaveFailed;
        }
    }

    private bool CanWrite => IsValid && SelectedStick is not null;

    /// <summary>Schreibt die Antwortdatei auf den Stick – nur nach Bestätigung (Regel 6.1.1).</summary>
    [RelayCommand(CanExecute = nameof(CanWrite))]
    private async Task WriteToStickAsync()
    {
        if (SelectedStick is not { } stick)
        {
            return;
        }

        var message = string.Format(CultureInfo.CurrentCulture, Texts.Confirm_WriteStick, stick.DisplayName);
        if (stick.HasAnswerFile)
        {
            message += "\n\n" + Texts.Confirm_WriteStickReplace;
        }

        if (LanguageWarning is { } warning)
        {
            message += "\n\n" + warning;
        }

        message += "\n\n" + (WipeDisk ? Texts.Confirm_WriteStickWipe : Texts.Confirm_WriteStickNormal);
        if (!await _dialogs.ConfirmAsync(Texts.Confirm_WriteStickTitle, message))
        {
            return;
        }

        try
        {
            await WindowsStickFinder.WriteAnswerFileAsync(stick, BuildOptions(), CancellationToken.None);
            _logger.LogInformation("Antwortdatei auf Stick {Root} geschrieben", stick.Root);
            StatusMessage = string.Format(CultureInfo.CurrentCulture, Texts.Page_StickWritten, stick.DisplayName);
            RefreshSticks();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Antwortdatei konnte nicht auf den Stick geschrieben werden");
            StatusMessage = Texts.Page_SaveFailed;
        }
    }

    private StickOptions BuildOptions() => new()
    {
        UiLanguage = SelectedLanguage.Value,
        UserLocale = SelectedFormat.Value,
        InputLocale = SelectedKeyboard.Value,
        TimeZone = SelectedTimeZone.Value,
        Edition = Enum.Parse<WindowsEdition>(SelectedEdition.Value),
        ComputerName = ComputerName.Trim(),
        AccountName = AccountName.Trim(),
        Password = Password,
        Disk = WipeDisk ? DiskMode.WipeFirstDisk : DiskMode.ChooseInSetup,
        // Der Name bleibt, wie er ist: Leerzeichen am Rand können zum WLAN-Namen gehören
        WifiName = WifiName,
        WifiPassword = WifiName.Length > 0 ? WifiPassword : string.Empty,
        PackageIds = Catalog.SelectedIds(),
        RemoveApps = RemovableApps.Where(app => app.IsSelected).Select(app => app.Choice.Value).ToList(),
        PrivacyDefaults = PrivacyDefaults,
        Desktop = new DesktopSettings
        {
            ShowFileExtensions = ShowFileExtensions,
            TaskbarLeft = TaskbarLeft,
            HideWidgets = HideWidgets,
            ClassicContextMenu = ClassicContextMenu,
            NoWebSearch = NoWebSearch,
        },
        Machine = new MachineSettings
        {
            PasswordNeverExpires = PasswordNeverExpires,
            DisableFastStartup = DisableFastStartup,
            PreventDeviceEncryption = PreventDeviceEncryption,
            BypassRequirements = BypassRequirements,
        },
    };

    private void Validate()
    {
        if (!_ready)
        {
            return;
        }

        var options = BuildOptions();
        var errors = StickOptionsValidator.Validate(options).ToList();
        if (Password != PasswordRepeat)
        {
            errors.Add(Texts.Error_PasswordRepeat);
        }

        if (WipeDisk && !WipeConfirmed)
        {
            errors.Add(Texts.Error_WipeNotConfirmed);
        }

        ErrorText = errors.Count > 0 ? string.Join("\n", errors.Select(error => "• " + error)) : null;
        IsValid = errors.Count == 0;
        Preview = IsValid ? AnswerFile.Create(options) : null;
    }

    private static Choice Pick(IReadOnlyList<Choice> choices, string? wanted, string fallback) =>
        choices.FirstOrDefault(choice => string.Equals(choice.Value, wanted, StringComparison.OrdinalIgnoreCase))
        ?? choices.First(choice => choice.Value == fallback);

    /// <summary>Passende Tastatur zum Format: Schweiz hat eine eigene, sonst nach Sprache.</summary>
    private static string KeyboardFor(string format) => format switch
    {
        "de-CH" => "0807:00000807",
        "en-GB" => "0809:00000809",
        _ when format.StartsWith("de", StringComparison.Ordinal) => "0407:00000407",
        _ when format.StartsWith("it", StringComparison.Ordinal) => "0410:00000410",
        _ when format.StartsWith("fr", StringComparison.Ordinal) => "040c:0000040c",
        _ => "0409:00000409",
    };

    /// <summary>Edition dieses PCs als Vorschlag – die Lizenz passt meist nur zu dieser Edition.</summary>
    private static WindowsEdition EditionOf(string? osName) => osName switch
    {
        not null when osName.Contains("Pro Education", StringComparison.OrdinalIgnoreCase) => WindowsEdition.ProEducation,
        not null when osName.Contains("Education", StringComparison.OrdinalIgnoreCase) => WindowsEdition.Education,
        not null when osName.Contains("Home", StringComparison.OrdinalIgnoreCase) => WindowsEdition.Home,
        _ => WindowsEdition.Pro,
    };
}
