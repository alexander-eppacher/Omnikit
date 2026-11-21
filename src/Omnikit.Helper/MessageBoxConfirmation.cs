using System.Runtime.InteropServices;
using Omnikit.Core.Elevation;

namespace Omnikit.Helper;

/// <summary>
/// Rückfrage als Windows-Meldungsfenster des Hilfsprozesses (mit Adminrechten). Vorgewählt ist „Nein“;
/// Enter oder Schließen bricht ab. Das Fenster kommt in den Vordergrund und bleibt über anderen Fenstern.
/// </summary>
internal sealed class MessageBoxConfirmation : IDestructiveConfirmation
{
    private const uint YesNo = 0x4;
    private const uint IconWarning = 0x30;
    private const uint DefaultSecondButton = 0x100;   // „Nein“ ist vorgewählt
    private const uint SetForeground = 0x10000;
    private const uint TopMost = 0x40000;
    private const int Yes = 6;

    public bool Confirm(string title, string message) =>
        MessageBoxW(IntPtr.Zero, message, title, YesNo | IconWarning | DefaultSecondButton | SetForeground | TopMost) == Yes;

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);
}
