using PcTool.Modules.Health.Sources;

namespace PcTool.Modules.Health.Tests.Sources;

public class BatteryReportParserTests
{
    // Aufbau wie in einem echten powercfg-Bericht (gekürzt). Weiter unten im echten Bericht gibt es
    // weitere DesignCapacity-Elemente in anderen Abschnitten, die NICHT als Akku gelesen werden dürfen.
    private const string Report = """
        <?xml version="1.0" encoding="utf-8"?>
        <BatteryReport xmlns="http://schemas.microsoft.com/battery/2012">
          <Batteries>
            <Battery>
              <Id>L23B3PG1</Id>
              <Manufacturer>Test</Manufacturer>
              <SerialNumber>1234</SerialNumber>
              <DesignCapacity>45000</DesignCapacity>
              <FullChargeCapacity>42090</FullChargeCapacity>
              <CycleCount>179</CycleCount>
            </Battery>
          </Batteries>
          <RuntimeEstimates>
            <DesignCapacity>
              <ActiveRuntime>PT10H</ActiveRuntime>
            </DesignCapacity>
          </RuntimeEstimates>
          <History>
            <HistoryEntry DesignCapacity="45000" FullChargeCapacity="41860" CycleCount="159" />
          </History>
        </BatteryReport>
        """;

    [Fact]
    public void ReadsBatteryValues()
    {
        var battery = Assert.Single(BatteryReportParser.Parse(Report));

        Assert.Equal("L23B3PG1", battery.Name);
        Assert.Equal(45000, battery.DesignCapacity);
        Assert.Equal(42090, battery.FullChargeCapacity);
        Assert.Equal(179, battery.CycleCount);
    }

    [Fact]
    public void ReportWithoutBatteryGivesEmptyList()
    {
        const string desktopReport = """
            <BatteryReport xmlns="http://schemas.microsoft.com/battery/2012"><Batteries /></BatteryReport>
            """;

        Assert.Empty(BatteryReportParser.Parse(desktopReport));
    }

    [Fact]
    public void MissingOrInvalidValuesBecomeNull()
    {
        const string report = """
            <BatteryReport><Batteries><Battery><DesignCapacity>abc</DesignCapacity></Battery></Batteries></BatteryReport>
            """;

        var battery = Assert.Single(BatteryReportParser.Parse(report));

        Assert.Null(battery.DesignCapacity);
        Assert.Null(battery.FullChargeCapacity);
        Assert.Null(battery.CycleCount);
    }

    [Fact]
    public void TwoBatteriesAreBothRead()
    {
        const string report = """
            <BatteryReport><Batteries>
              <Battery><DesignCapacity>1</DesignCapacity></Battery>
              <Battery><DesignCapacity>2</DesignCapacity></Battery>
            </Batteries></BatteryReport>
            """;

        Assert.Equal(2, BatteryReportParser.Parse(report).Count);
    }
}
