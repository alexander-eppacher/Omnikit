using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Omnikit.Agent;
using Omnikit.Agent.Resources;
using Omnikit.Core.Elevation;
using Omnikit.Core.Modules;
using Omnikit.Core.SystemInfo;
using Omnikit.Modules.Apps.Winget;
using Omnikit.Modules.Bios;
using Omnikit.Modules.Bios.Sources;
using Omnikit.Modules.Drivers;
using Omnikit.Modules.Drivers.Sources;
using Omnikit.Modules.Health;
using Omnikit.Modules.Health.Sources;

// Omnikit-Agent. Befehle: register, install, uninstall, run (so startet ihn Windows als Dienst), once.
var command = AgentCommand.Parse(args);
switch (command)
{
    case AgentCommand.Help help:
        if (help.Error is not null)
        {
            Console.Error.WriteLine(help.Error);
        }

        Console.WriteLine(AgentTexts.Help);
        return help.Error is null ? 0 : 2;

    case AgentCommand.Register register:
        return await RegisterAsync(register);

    case AgentCommand.Install:
        if (!RequireAdmin() || !RequireRegistration())
        {
            return 1;
        }

        try
        {
            Console.WriteLine(string.Format(CultureInfo.CurrentCulture, AgentTexts.Install_Done, await ServiceInstaller.InstallAsync()));
            return 0;
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture, AgentTexts.Install_Failed, ex.Message));
            return 1;
        }

    case AgentCommand.Uninstall:
        if (!RequireAdmin())
        {
            return 1;
        }

        try
        {
            await ServiceInstaller.UninstallAsync();
        }
        catch (InvalidOperationException ex)
        {
            Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture, AgentTexts.Uninstall_Failed, ex.Message));
            return 1;
        }

        AgentStore.Default.Delete();
        Console.WriteLine(string.Format(CultureInfo.CurrentCulture, AgentTexts.Uninstall_Done, AgentStore.InstallDirectory));
        return 0;

    case AgentCommand.Once:
    case AgentCommand.Run:
        return await RunAgentAsync(asService: command is AgentCommand.Run);

    default:
        return 2;
}

/// <summary>Als Dienst laufen oder einmal melden und Aufgaben abarbeiten („once“).</summary>
static async Task<int> RunAgentAsync(bool asService)
{
    // Die Anmeldedaten dürfen nur Administratoren und SYSTEM lesen – ohne Adminrechte sähe es aus wie „nicht registriert“
    if (!RequireAdmin() || LoadConfigOrExplain() is not { } config)
    {
        return 1;
    }

    var token = AgentStore.Default.LoadToken();
    if (token is null)
    {
        Console.Error.WriteLine(AgentTexts.NotRegistered);
        return 1;
    }

    // Dienst und „once“ nie gleichzeitig: Sonst holen beide Aufgaben ab und installieren parallel
    using var agentLock = AgentLock.TryAcquire();
    if (agentLock is null)
    {
        Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture, AgentTexts.AlreadyRunning, ServiceInstaller.ServiceName));
        return 1;
    }

    using var host = BuildHost(config, token, asService);
    if (asService)
    {
        await host.RunAsync();
        return 0;
    }

    var cycle = host.Services.GetRequiredService<AgentCycle>();
    await cycle.SendReportAsync(CancellationToken.None);
    await cycle.RunTasksAsync(CancellationToken.None);
    Console.WriteLine(AgentTexts.Once_Done);
    return 0;
}

static async Task<int> RegisterAsync(AgentCommand.Register register)
{
    if (!RequireAdmin())
    {
        return 1;
    }

    if (register.CaFile is not null && !File.Exists(register.CaFile))
    {
        Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture, AgentTexts.Ca_NotFound, register.CaFile));
        return 1;
    }

    if (register.CaFile is not null && !IsRootCertificateFile(register.CaFile))
    {
        Console.Error.WriteLine(AgentTexts.Ca_NotRoot);
        return 1;
    }

    try
    {
        using var client = new ServerClient(register.Server, token: null, register.CaFile);
        var response = await client.RegisterAsync(register.Code, Environment.MachineName, CancellationToken.None);
        if (response is null)
        {
            Console.Error.WriteLine(AgentTexts.Register_Refused);
            return 1;
        }

        AgentStore.Default.Save(new AgentConfig(register.Server, response.DeviceId, register.CaFile is not null), response.Token, register.CaFile);
        Console.WriteLine(string.Format(CultureInfo.CurrentCulture, AgentTexts.Register_Done, response.DeviceId));
        return 0;
    }
    catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
    {
        Console.Error.WriteLine(AgentTexts.Register_TooMany);
        return 1;
    }
    catch (HttpRequestException ex)
    {
        Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture, AgentTexts.Register_Unreachable, ex.Message));
        return 1;
    }
}

static bool IsRootCertificateFile(string path)
{
    try
    {
        using var certificate = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificateFromFile(path);
        return ServerClient.IsRootCertificate(certificate);
    }
    catch (System.Security.Cryptography.CryptographicException)
    {
        return false; // gar kein Zertifikat
    }
}

static bool RequireAdmin()
{
    if (ServiceInstaller.IsAdministrator())
    {
        return true;
    }

    Console.Error.WriteLine(AgentTexts.NeedAdmin);
    return false;
}

static bool RequireRegistration() => LoadConfigOrExplain() is not null;

/// <summary>Einstellungen laden; fehlen sie oder sind sie beschädigt, steht der Grund in der Konsole.</summary>
static AgentConfig? LoadConfigOrExplain()
{
    try
    {
        if (AgentStore.Default.LoadConfig() is { } config)
        {
            return config;
        }

        Console.Error.WriteLine(AgentTexts.NotRegistered);
    }
    catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(string.Format(CultureInfo.CurrentCulture, AgentTexts.Config_Broken, ex.Message));
    }

    return null;
}

static IHost BuildHost(AgentConfig config, string token, bool asService)
{
    var builder = Host.CreateApplicationBuilder();
    if (asService)
    {
        // Als Windows-Dienst: Start/Stopp über Windows, Meldungen in die Ereignisanzeige („Anwendung“)
        builder.Services.AddWindowsService(options => options.ServiceName = ServiceInstaller.ServiceName);
        builder.Services.AddHostedService<AgentWorker>();
    }

    var services = builder.Services;
    services.AddSingleton(TimeProvider.System);
    services.AddSingleton<ISystemInfoProvider>(provider => OperatingSystem.IsWindows()
        ? new WindowsSystemInfoProvider(provider.GetRequiredService<ILogger<WindowsSystemInfoProvider>>())
        : new UnavailableSystemInfoProvider());
    services.AddSingleton<SystemContextBuilder>();
    services.AddSingleton<IElevationRunner, NoElevationRunner>();

    // Dieselben Module wie in der App (nur lesen)
    services.AddSingleton<WindowsDiskSource>();
    services.AddSingleton<IDiskSource>(provider => provider.GetRequiredService<WindowsDiskSource>());
    services.AddSingleton<IDiskReliabilitySource>(provider => provider.GetRequiredService<WindowsDiskSource>());
    services.AddSingleton<IVolumeSource, VolumeSource>();
    services.AddSingleton<IBatterySource, PowercfgBatterySource>();
    services.AddSingleton<IWindowsStateSource, WindowsStateSource>();
    services.AddSingleton<IUpdateSource, WindowsUpdateSource>();
    services.AddSingleton<IDeviceSource, WindowsDeviceSource>();
    services.AddSingleton<IFirmwareSource, WindowsFirmwareSource>();
    services.AddSingleton<IModule, HealthModule>();
    services.AddSingleton<IModule, DriversModule>();
    services.AddSingleton<IModule, BiosModule>();

    // winget: als SYSTEM aus dem Ordner des App-Installers
    services.AddSingleton<IWingetRunner>(_ => new WingetRunner(WingetLocator.Find));
    services.AddSingleton<IWingetClient, WingetClient>();

    services.AddSingleton(_ => new ServerClient(config.Server, token, config.HasCustomCa ? AgentStore.Default.CaFile : null));
    services.AddSingleton<IAgentServer>(provider => provider.GetRequiredService<ServerClient>());
    services.AddSingleton(provider => new TaskLoop(
        provider.GetRequiredService<IAgentServer>(), provider.GetRequiredService<TaskRunner>(),
        provider.GetRequiredService<ILogger<TaskLoop>>(), TimeSpan.FromSeconds(20)));
    services.AddSingleton<ReportBuilder>();
    services.AddSingleton<TaskRunner>();
    services.AddSingleton<AgentCycle>();
    return builder.Build();
}
