namespace PcTool.Desktop.ViewModels;

/// <summary>
/// Eine Seite, die nur arbeiten soll, solange sie angezeigt wird (z. B. die Live-Messung auf „Leistung“).
/// Das Hauptfenster ruft <see cref="Activate"/> beim Öffnen und <see cref="Deactivate"/> beim Verlassen der Seite auf.
/// </summary>
public interface IActivatablePage
{
    void Activate();

    void Deactivate();
}
