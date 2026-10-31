using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Omnikit.Core.Modules;
using Omnikit.Core.Settings;
using Omnikit.Core.SystemInfo;
using Omnikit.Modules.Performance.Data;
using Omnikit.Modules.Performance.Monitoring;
using Omnikit.Modules.Performance.Sources;
using static Omnikit.Modules.Performance.Monitoring.Units;
using Texts = Omnikit.Modules.Performance.Resources.PerformanceTexts;

namespace Omnikit.Desktop.ViewModels;

/// <summary>
/// Seite „Leistung“: misst jede Sekunde, aber nur solange die Seite offen ist (<see cref="Activate"/>/<see cref="Deactivate"/>).
/// Gemessen wird im Hintergrund; die Anzeige wird danach im UI-Thread aktualisiert.
/// </summary>
public sealed partial class PerformancePageViewModel : ViewModelBase, IActivatablePage, IDisposable
{
    private const int CpuOrder = 0;
    private const int MemoryOrder = 1;
    private const int DiskOrder = 10;
    private const int GpuOrder = 100;
    private const int NetworkOrder = 200;

    private readonly IPerformanceSource _source;
    private readonly IHardwareInventorySource _inventorySource;
    private readonly ViewModeState _viewMode;
    private readonly ILogger _logger;

    private readonly Dictionary<string, LoadHistory> _histories = [];
    private readonly List<LoadHistory> _coreHistories = [];
    private readonly Dictionary<string, UsageCardViewModel> _cards = [];
    private readonly Dictionary<string, NetworkSample> _networks = [];

    private HardwareInventory? _inventory;
    private CancellationTokenSource? _cancellation;
    private Task _loop = Task.CompletedTask;

    public PerformancePageViewModel(
        IPerformanceSource source, IHardwareInventorySource inventorySource, ViewModeState viewMode, SystemContext context, ILogger logger)
    {
        _source = source;
        _inventorySource = inventorySource;
        _viewMode = viewMode;
        _logger = logger;
        IsSupported = context.IsWindows;

        _viewMode.PropertyChanged += (_, _) => ApplyViewMode();
    }

    public bool IsSupported { get; }

    public ObservableCollection<UsageCardViewModel> Cards { get; } = [];

    [ObservableProperty]
    public partial Severity? SummarySeverity { get; set; }

    [ObservableProperty]
    public partial string? SummaryText { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    /// <summary>Hinweis, wenn keine Grafikkarte Messwerte liefert (z. B. in einer VM).</summary>
    [ObservableProperty]
    public partial bool ShowNoGpu { get; set; }

    public void Activate()
    {
        if (!IsSupported || _cancellation is not null)
        {
            return;
        }

        foreach (var history in _histories.Values.Concat(_coreHistories))
        {
            history.Clear();
        }

        _cancellation = new CancellationTokenSource();
        var token = _cancellation.Token;
        var previous = _loop;

        // Erst warten, bis eine vorherige Messung ganz beendet ist; sie schließt die gemeinsame Messquelle.
        _loop = Task.Run(async () =>
        {
            await previous.ConfigureAwait(false);
            await MeasureAsync(token).ConfigureAwait(false);
        });
    }

    public void Deactivate()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
    }

    public void Dispose() => Deactivate();

    private async Task MeasureAsync(CancellationToken cancellationToken)
    {
        try
        {
            _inventory ??= _inventorySource.Read();
            _source.Open();

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
            {
                var sample = _source.Read();
                Dispatcher.UIThread.Post(() => Apply(sample));
            }
        }
        catch (OperationCanceledException)
        {
            // Seite wurde verlassen
        }
#pragma warning disable CA1031 // Ein Messfehler soll verständlich angezeigt werden, nicht die App beenden.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Leistungsmessung fehlgeschlagen");
            Dispatcher.UIThread.Post(() => ErrorMessage = Texts.ReadFailed);
        }
        finally
        {
            _source.Close();
        }
    }

    private void Apply(PerformanceSample sample)
    {
        var inventory = _inventory ?? HardwareInventory.Empty;
        var cpuHistory = ApplyCpu(sample, inventory);
        var memoryHistory = ApplyMemory(sample, inventory);
        var busyDisks = ApplyDisks(sample, inventory);
        ApplyGpus(sample, inventory);
        ApplyNetworks(sample);

        var summary = PerformanceSummary.Build(
            LoadRating.HasEnoughData(cpuHistory),
            LoadRating.Rate(cpuHistory) != Severity.Ok,
            memoryHistory is not null && LoadRating.Rate(memoryHistory) != Severity.Ok,
            busyDisks);
        SummarySeverity = summary.Severity;
        SummaryText = summary.Text;
        ApplyViewMode();
    }

    private LoadHistory ApplyCpu(PerformanceSample sample, HardwareInventory inventory)
    {
        var history = History("cpu");
        var card = Card("cpu", CpuOrder);
        var cpu = sample.Cpu;
        var info = inventory.Cpu;

        if (cpu.TotalPercent is double percent)
        {
            history.Add(percent);
            card.ValueText = Percent(percent);
            card.Word = LoadRating.Describe(percent);
        }
        else
        {
            card.ValueText = Texts.NotAvailable;
        }

        card.Title = Texts.Cpu_Title;
        card.Subtitle = info?.Name;
        card.DetailText = cpu.CurrentMhz is double mhz ? Megahertz(mhz) : null;
        card.Severity = LoadRating.Rate(history);
        card.History = history.ToArray();

        card.SetExpertRows(Rows(
            (Texts.Expert_Speed, cpu.CurrentMhz is double current ? Megahertz(current) : null),
            (Texts.Expert_BaseSpeed, info?.BaseMhz is int baseMhz ? Megahertz(baseMhz) : null),
            (Texts.Expert_Sockets, info?.Sockets.ToString(System.Globalization.CultureInfo.CurrentCulture)),
            (Texts.Expert_Cores, info?.Cores?.ToString(System.Globalization.CultureInfo.CurrentCulture)),
            (Texts.Expert_Logical, info?.LogicalProcessors?.ToString(System.Globalization.CultureInfo.CurrentCulture)),
            (Texts.Expert_Virtualization, Virtualization(info)),
            (Texts.Expert_L1, info?.L1CacheBytes is long l1 ? Bytes(l1) : null),
            (Texts.Expert_L2, info?.L2CacheBytes is long l2 ? Bytes(l2) : null),
            (Texts.Expert_L3, info?.L3CacheBytes is long l3 ? Bytes(l3) : null),
            (Texts.Expert_Processes, sample.Counts?.Processes.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)),
            (Texts.Expert_Threads, sample.Counts?.Threads.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)),
            (Texts.Expert_Handles, sample.Counts?.Handles.ToString("N0", System.Globalization.CultureInfo.CurrentCulture)),
            (Texts.Expert_Uptime, Uptime(sample.Uptime))));

        // Verlauf je logischem Prozessor
        while (_coreHistories.Count < cpu.CorePercents.Count)
        {
            _coreHistories.Add(new LoadHistory());
        }

        for (var i = 0; i < cpu.CorePercents.Count; i++)
        {
            _coreHistories[i].Add(cpu.CorePercents[i]);
        }

        card.SetCores(_coreHistories.Take(cpu.CorePercents.Count).Select(core => core.ToArray()).ToList());
        return history;
    }

    private LoadHistory? ApplyMemory(PerformanceSample sample, HardwareInventory inventory)
    {
        if (sample.Memory is not MemorySample memory)
        {
            return null;
        }

        var history = History("memory");
        var card = Card("memory", MemoryOrder);
        var info = inventory.Memory;
        history.Add(memory.UsedPercent);

        card.Title = Texts.Memory_Title;
        card.Subtitle = Naming.ReservedMemoryNote(info?.InstalledBytes, memory.TotalBytes);
        card.ValueText = Percent(memory.UsedPercent);
        card.Word = LoadRating.DescribeMemory(memory.UsedPercent);
        card.DetailText = Format(Texts.Memory_Value, Bytes(memory.UsedBytes), Bytes(memory.TotalBytes));
        card.Severity = LoadRating.Rate(history);
        card.History = history.ToArray();

        card.SetExpertRows(Rows(
            (Texts.Expert_Available, Bytes(memory.AvailableBytes)),
            (Texts.Expert_Usable, Bytes(memory.TotalBytes)),
            (Texts.Expert_Installed, info?.InstalledBytes is long installed ? Bytes(installed) : null),
            (Texts.Expert_Committed, memory is { CommittedBytes: long committed, CommitLimitBytes: long limit }
                ? Format(Texts.Expert_MemoryUsedOf, Bytes(committed), Bytes(limit))
                : null),
            (Texts.Expert_MemorySpeed, info?.SpeedMts is int speed ? Format(Texts.Expert_MemorySpeedValue, speed) : null),
            (Texts.Expert_Slots, info is { SlotsUsed: int used, SlotsTotal: int total } ? Format(Texts.Expert_SlotsValue, used, total) : null)));
        return history;
    }

    /// <summary>Aktualisiert die Laufwerkskarten und liefert die Laufwerke, die seit 30 Sekunden fast durchgehend beschäftigt sind.</summary>
    private List<string> ApplyDisks(PerformanceSample sample, HardwareInventory inventory)
    {
        var busy = new List<string>();
        foreach (var disk in sample.Disks)
        {
            var key = $"disk-{disk.Number}";
            var history = History(key);
            var card = Card(key, DiskOrder + disk.Number);
            var info = inventory.Disks.FirstOrDefault(d => d.Number == disk.Number);
            history.Add(disk.ActivePercent);

            card.Title = Naming.Disk(disk, info?.Kind ?? DiskKind.Unknown);
            card.Subtitle = info?.Model;
            card.ValueText = Percent(disk.ActivePercent);
            card.Word = LoadRating.Describe(disk.ActivePercent);
            card.DetailText = Format(Texts.Disk_Value, BytesPerSecond(disk.ReadBytesPerSecond), BytesPerSecond(disk.WriteBytesPerSecond));
            card.Severity = LoadRating.Rate(history);
            card.History = history.ToArray();

            card.SetExpertRows(Rows(
                (Texts.Expert_Model, info?.Model),
                (Texts.Expert_Type, info?.Kind switch { DiskKind.Ssd => Texts.Disk_Ssd, DiskKind.Hdd => Texts.Disk_Hdd, _ => null }),
                (Texts.Expert_ActiveTime, Percent(disk.ActivePercent)),
                (Texts.Expert_Read, BytesPerSecond(disk.ReadBytesPerSecond)),
                (Texts.Expert_Write, BytesPerSecond(disk.WriteBytesPerSecond))));

            if (card.Severity != Severity.Ok)
            {
                busy.Add(Naming.DiskShort(disk));
            }
        }

        RemoveMissing("disk-", sample.Disks.Select(disk => $"disk-{disk.Number}"));
        return busy;
    }

    private void ApplyGpus(PerformanceSample sample, HardwareInventory inventory)
    {
        // Nur echte Grafikkarten (laut DXGI). Ohne DXGI-Daten alle, die Messwerte liefern.
        var gpus = inventory.Gpus.Count > 0
            ? sample.Gpus.Where(gpu => inventory.Gpus.Any(info => info.Luid == gpu.Luid)).ToList()
            : sample.Gpus.ToList();

        for (var i = 0; i < gpus.Count; i++)
        {
            var gpu = gpus[i];
            var key = $"gpu-{gpu.Luid}";
            var history = History(key);
            var card = Card(key, GpuOrder + i);
            var info = inventory.Gpus.FirstOrDefault(g => g.Luid == gpu.Luid);
            history.Add(gpu.UtilizationPercent);

            card.Title = gpus.Count > 1 ? $"{Texts.Gpu_Title} {i + 1}" : Texts.Gpu_Title;
            card.Subtitle = info?.Name;
            card.ValueText = Percent(gpu.UtilizationPercent);
            card.Word = LoadRating.Describe(gpu.UtilizationPercent);
            card.DetailText = gpu.TemperatureCelsius is double celsius ? Format(Texts.Expert_TemperatureValue, celsius) : null;
            card.Severity = null; // Grafik wird nicht bewertet: Spiele dürfen die GPU voll auslasten
            card.History = history.ToArray();

            card.SetExpertRows(Rows(
                (Texts.Expert_DedicatedMemory, MemoryOf(gpu.DedicatedUsedBytes, info?.DedicatedMemoryBytes)),
                (Texts.Expert_SharedMemory, MemoryOf(gpu.SharedUsedBytes, info?.SharedMemoryBytes)),
                (Texts.Expert_Temperature, gpu.TemperatureCelsius is double temperature ? Format(Texts.Expert_TemperatureValue, temperature) : null)));
        }

        RemoveMissing("gpu-", gpus.Select(gpu => $"gpu-{gpu.Luid}"));
        ShowNoGpu = gpus.Count == 0;
    }

    private void ApplyNetworks(PerformanceSample sample)
    {
        _networks.Clear();
        var index = 0;
        foreach (var adapter in sample.Networks.OrderBy(a => a.IsVirtual).ThenByDescending(a => a.IsConnected).ThenBy(a => a.Name, StringComparer.CurrentCulture))
        {
            var key = $"net-{adapter.Id}";
            var total = adapter.SendBitsPerSecond + adapter.ReceiveBitsPerSecond;
            var history = History(key);
            var card = Card(key, NetworkOrder + index++);
            _networks[key] = adapter;
            history.Add(total);

            card.Title = Format(Texts.Network_Title, adapter.Name);
            card.Subtitle = !adapter.IsConnected ? Texts.Network_Disconnected : adapter.IsWireless ? Texts.Network_Wireless : Texts.Network_Wired;
            card.ValueText = BitsPerSecond(total);
            card.Word = null;
            card.DetailText = Format(Texts.Network_Value, BitsPerSecond(adapter.SendBitsPerSecond), BitsPerSecond(adapter.ReceiveBitsPerSecond));
            card.Severity = null; // Netzwerk wird nicht bewertet: Downloads dürfen die Leitung auslasten
            card.GraphMaximum = 0;
            card.History = history.ToArray();

            card.SetExpertRows(Rows(
                (Texts.Expert_Adapter, adapter.Description),
                (Texts.Expert_LinkSpeed, adapter.LinkSpeedBitsPerSecond is long speed ? BitsPerSecond(speed) : null),
                (Texts.Expert_Send, BitsPerSecond(adapter.SendBitsPerSecond)),
                (Texts.Expert_Receive, BitsPerSecond(adapter.ReceiveBitsPerSecond)),
                (Texts.Expert_IpAddresses, adapter.IpAddresses.Count > 0 ? string.Join(", ", adapter.IpAddresses) : null),
                (Texts.Expert_Virtual, adapter.IsVirtual ? Texts.Expert_Yes : null)));
        }

        RemoveMissing("net-", _networks.Keys);
    }

    /// <summary>Ansicht umgeschaltet oder neue Werte: Expertenteile und virtuelle/getrennte Netzwerkadapter ein- oder ausblenden.</summary>
    private void ApplyViewMode()
    {
        var expert = _viewMode.Mode == ViewMode.Expert;
        foreach (var card in Cards)
        {
            card.ShowExpert = expert;
            card.IsVisible = expert || !_networks.TryGetValue(card.Key, out var adapter) || NetworkAdapterFilter.IsShownInSimpleView(adapter);
        }
    }

    private LoadHistory History(string key)
    {
        if (!_histories.TryGetValue(key, out var history))
        {
            history = new LoadHistory();
            _histories[key] = history;
        }

        return history;
    }

    /// <summary>Holt die Karte zum Schlüssel oder legt sie an der richtigen Stelle an.</summary>
    private UsageCardViewModel Card(string key, int order)
    {
        if (_cards.TryGetValue(key, out var card))
        {
            return card;
        }

        card = new UsageCardViewModel(key, order);
        _cards[key] = card;
        var position = Cards.TakeWhile(existing => existing.Order <= order).Count();
        Cards.Insert(position, card);
        return card;
    }

    /// <summary>Entfernt Karten von Geräten, die verschwunden sind (z. B. abgezogener USB-Stick).</summary>
    private void RemoveMissing(string prefix, IEnumerable<string> presentKeys)
    {
        var present = presentKeys.ToHashSet();
        foreach (var key in _cards.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal) && !present.Contains(key)).ToList())
        {
            Cards.Remove(_cards[key]);
            _cards.Remove(key);
            _histories.Remove(key);
        }
    }

    /// <summary>Nur Zeilen mit Wert; fehlende Werte werden weggelassen statt „nicht verfügbar“ zu zeigen.</summary>
    private static List<(string Label, string Value)> Rows(params (string Label, string? Value)[] rows) =>
        rows.Where(row => row.Value is not null).Select(row => (row.Label, row.Value!)).ToList();

    private static string? Virtualization(CpuInfo? info) => info switch
    {
        { HypervisorPresent: true } => Texts.Expert_HypervisorPresent,
        { VirtualizationEnabled: true } => Texts.Expert_VirtualizationOn,
        { VirtualizationEnabled: false } => Texts.Expert_VirtualizationOff,
        _ => null,
    };

    private static string? MemoryOf(long? used, long? total) => (used, total) switch
    {
        (long u, long t) when t > 0 => Format(Texts.Expert_MemoryUsedOf, Bytes(u), Bytes(t)),
        (long u, _) => Bytes(u),
        _ => null,
    };
}
