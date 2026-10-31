namespace Omnikit.Core.Modules;

/// <summary>
/// Schweregrad eines Befunds. Wird in der UI als Ampel angezeigt.
/// Die Reihenfolge ist wichtig: höherer Wert = schlimmer.
/// </summary>
public enum Severity
{
    Ok = 0,
    Info = 1,
    Warning = 2,
    Critical = 3,
}
