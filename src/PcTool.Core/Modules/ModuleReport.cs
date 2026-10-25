namespace PcTool.Core.Modules;

/// <summary>
/// Ergebnis von <see cref="IModule.CheckAsync"/>: alle Befunde eines Moduls.
/// </summary>
public sealed record ModuleReport(string ModuleId, IReadOnlyList<Finding> Findings)
{
    /// <summary>
    /// Gesamtampel: der schlimmste Einzelbefund. Ohne Befunde ist alles in Ordnung.
    /// </summary>
    public Severity OverallSeverity =>
        Findings.Count == 0 ? Severity.Ok : Findings.Max(finding => finding.Severity);
}
