namespace Omnikit.Agent;

/// <summary>
/// Sorgt dafür, dass auf einem PC immer nur ein Agent arbeitet (Dienst oder „once“). Systemweit benannter Mutex;
/// gehalten bis zum Ende des Prozesses (Windows gibt ihn dann frei, auch nach einem Absturz).
/// </summary>
public static class AgentLock
{
    private const string Name = @"Global\Omnikit.Agent";

    /// <returns>Der gehaltene Mutex oder <c>null</c>, wenn schon ein anderer Agent läuft.</returns>
    public static IDisposable? TryAcquire()
    {
        Mutex mutex;
        try
        {
            mutex = new Mutex(initiallyOwned: false, Name);
        }
        catch (UnauthorizedAccessException)
        {
            return null; // vom Dienst (SYSTEM) angelegt und für andere nicht zu öffnen: der Dienst läuft
        }

        try
        {
            if (mutex.WaitOne(TimeSpan.Zero))
            {
                return mutex;
            }
        }
        catch (AbandonedMutexException)
        {
            return mutex; // der vorige Agent ist abgestürzt – jetzt gehört er uns
        }

        mutex.Dispose();
        return null;
    }
}
