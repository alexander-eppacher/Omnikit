using Omnikit.Core.Modules;
using Omnikit.Core.Settings;

namespace Omnikit.Core.Tests.Modules;

public class ViewModeFilterTests
{
    private static readonly Finding Normal = new("Zustand", "Alles gut.", Severity.Ok, "Laufwerke", Details: "Firmware: 1.0");
    private static readonly Finding ExpertOnly = new("Betriebsstunden", "100 Stunden.", Severity.Ok, "Laufwerke", IsExpertOnly: true);

    [Fact]
    public void ExpertSeesEverythingIncludingDetails()
    {
        var shown = ViewModeFilter.Apply([Normal, ExpertOnly], ViewMode.Expert);

        Assert.Equal([Normal, ExpertOnly], shown);
    }

    [Fact]
    public void SimpleHidesExpertOnlyFindings()
    {
        var shown = ViewModeFilter.Apply([Normal, ExpertOnly], ViewMode.Simple);

        Assert.Equal("Zustand", Assert.Single(shown).Title);
    }

    [Fact]
    public void SimpleDropsDetailsButKeepsTheRest()
    {
        var shown = Assert.Single(ViewModeFilter.Apply([Normal], ViewMode.Simple));

        Assert.Null(shown.Details);
        Assert.Equal(Normal with { Details = null }, shown);
    }
}
