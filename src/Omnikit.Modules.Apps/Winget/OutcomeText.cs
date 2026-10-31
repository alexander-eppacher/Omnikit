using Texts = Omnikit.Modules.Apps.Resources.AppsTexts;

namespace Omnikit.Modules.Apps.Winget;

/// <summary>Verständlicher Satz zu jedem Ergebnis. Der Fehlercode steht nur in der Expertenansicht und im Log.</summary>
public static class OutcomeText
{
    public static string For(WingetOutcome outcome) => outcome switch
    {
        WingetOutcome.Success => Texts.Outcome_Success,
        WingetOutcome.SuccessRebootRequired => Texts.Outcome_SuccessRebootRequired,
        WingetOutcome.NothingToDo => Texts.Outcome_NothingToDo,
        WingetOutcome.NoNetwork => Texts.Outcome_NoNetwork,
        WingetOutcome.PackageNotFound => Texts.Outcome_PackageNotFound,
        WingetOutcome.AppInUse => Texts.Outcome_AppInUse,
        WingetOutcome.RebootRequiredFirst => Texts.Outcome_RebootRequiredFirst,
        WingetOutcome.Cancelled => Texts.Outcome_Cancelled,
        WingetOutcome.BlockedByPolicy => Texts.Outcome_BlockedByPolicy,
        WingetOutcome.DiskFull => Texts.Outcome_DiskFull,
        WingetOutcome.NotSupportedOnThisPc => Texts.Outcome_NotSupportedOnThisPc,
        WingetOutcome.WrongElevation => Texts.Outcome_WrongElevation,
        WingetOutcome.TimedOut => Texts.Outcome_TimedOut,
        _ => Texts.Outcome_Failed,
    };
}
