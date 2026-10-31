namespace Omnikit.Modules.Apps.Winget;

/// <summary>Startet winget mit den angegebenen Argumenten. Hinter einem Interface, damit Tests ohne Windows laufen.</summary>
public interface IWingetRunner
{
    /// <exception cref="WingetMissingException">winget ist auf diesem PC nicht installiert.</exception>
    Task<WingetRunResult> RunAsync(IReadOnlyList<string> arguments, TimeSpan timeout, CancellationToken cancellationToken);
}

/// <param name="ExitCode">Rückgabecode von winget (0 = Erfolg).</param>
/// <param name="Output">Alles, was winget ausgegeben hat.</param>
/// <param name="TimedOut">winget wurde nach Ablauf der Zeit beendet.</param>
public sealed record WingetRunResult(int ExitCode, string Output, bool TimedOut = false);

/// <summary>winget (Teil des „App-Installers“) fehlt, z. B. auf älteren oder stark abgespeckten Windows-Installationen.</summary>
public sealed class WingetMissingException : Exception
{
    public WingetMissingException()
        : base("winget wurde nicht gefunden.")
    {
    }

    public WingetMissingException(string message)
        : base(message)
    {
    }

    public WingetMissingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>Eine Abfrage (z. B. die Liste der Updates) ist fehlgeschlagen.</summary>
public sealed class WingetException : Exception
{
    public WingetException(WingetOutcome outcome, int exitCode)
        : base($"winget fehlgeschlagen: {outcome} ({WingetExitCodes.Format(exitCode)})")
    {
        Outcome = outcome;
        ExitCode = exitCode;
    }

    public WingetException()
    {
    }

    public WingetException(string message)
        : base(message)
    {
    }

    public WingetException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public WingetOutcome Outcome { get; } = WingetOutcome.Failed;

    public int ExitCode { get; }
}
