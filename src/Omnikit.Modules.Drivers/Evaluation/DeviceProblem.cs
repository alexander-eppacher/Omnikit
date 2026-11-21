namespace Omnikit.Modules.Drivers.Evaluation;

/// <summary>Fehlercodes des Geräte-Managers, nach dem gruppiert, was der Nutzer tun kann.</summary>
public enum DeviceProblem
{
    /// <summary>Kein Fehler oder ein Zustand, der nichts bedeutet (z. B. Gerät nicht angeschlossen).</summary>
    None,

    /// <summary>Code 28: Es ist gar kein Treiber installiert.</summary>
    NoDriver,

    /// <summary>Treiber beschädigt, unvollständig oder passt nicht.</summary>
    DriverBroken,

    /// <summary>Windows blockiert den Treiber (bekannte Probleme, Signatur).</summary>
    DriverBlocked,

    /// <summary>Das Gerät startet nicht oder hat selbst einen Fehler gemeldet.</summary>
    NotStarting,

    /// <summary>Zu wenig freie Ressourcen (meist BIOS oder ein anderes Gerät).</summary>
    Resources,

    /// <summary>Löst sich meist mit einem Neustart oder Ab- und Anstecken.</summary>
    RestartNeeded,

    /// <summary>Absichtlich ausgeschaltet.</summary>
    Disabled,

    /// <summary>Sonstiger Fehler.</summary>
    Other,
}

/// <summary>Ordnet die Fehlercodes ein (Microsoft-Dokumentation „Device Manager error messages“).</summary>
public static class DeviceProblems
{
    public static DeviceProblem Classify(int errorCode) => errorCode switch
    {
        // 45 = nicht angeschlossen, 46 = Windows fährt herunter, 53 = für den Kernel-Debugger reserviert
        0 or 45 or 46 or 53 => DeviceProblem.None,
        28 => DeviceProblem.NoDriver,
        1 or 3 or 18 or 19 or 31 or 32 or 37 or 39 or 40 or 41 or 50 => DeviceProblem.DriverBroken,
        48 or 52 => DeviceProblem.DriverBlocked,
        10 or 43 or 54 => DeviceProblem.NotStarting,
        12 or 16 or 33 or 34 or 36 => DeviceProblem.Resources,
        14 or 21 or 38 or 42 or 44 or 47 => DeviceProblem.RestartNeeded,
        22 => DeviceProblem.Disabled,
        _ => DeviceProblem.Other,
    };
}
