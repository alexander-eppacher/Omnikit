using System.Reflection;

namespace Omnikit.Agent;

/// <summary>Version dieses Agents, wie sie beim Bauen gesetzt wurde (Directory.Build.props).</summary>
public static class AgentVersion
{
    public static string Current { get; } = Clean(typeof(AgentVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

    /// <summary>„4.0.0-dev+abc123“ → „4.0.0-dev“ (der Commit-Hash gehört nicht in die Anzeige).</summary>
    public static string Clean(string? version) => version is null ? "?" : version.Split('+')[0];
}
