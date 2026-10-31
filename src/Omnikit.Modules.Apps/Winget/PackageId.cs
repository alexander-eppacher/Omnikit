using System.Text.RegularExpressions;

namespace Omnikit.Modules.Apps.Winget;

/// <summary>
/// Prüft winget-Paket-IDs wie „Mozilla.Firefox“ oder „Notepad++.Notepad++“.
/// Nur Buchstaben, Ziffern und . + - _ sind erlaubt: Damit kann eine ID nie als zusätzliche Option
/// („--…“) oder als anderer Befehl verstanden werden (Regel 6.1.4).
/// </summary>
public static partial class PackageId
{
    public static bool IsValid(string? id) => id is not null && Pattern().IsMatch(id);

    [GeneratedRegex(@"^[A-Za-z0-9][A-Za-z0-9.+_\-]{0,127}$")]
    private static partial Regex Pattern();
}
