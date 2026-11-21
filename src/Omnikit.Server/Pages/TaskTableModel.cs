using Omnikit.Server.Data;

namespace Omnikit.Server.Pages;

/// <summary>Daten für die Aufgabentabelle (auf „Aufgaben“ und bei jedem Gerät).</summary>
public sealed record TaskTableModel(IReadOnlyList<DeviceTask> Tasks, bool CanDecide, bool ShowDevice);
