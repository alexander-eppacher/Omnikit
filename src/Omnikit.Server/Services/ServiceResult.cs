namespace Omnikit.Server.Services;

/// <summary>Ergebnis einer Verwaltungsaktion. <see cref="Error"/> ist ein verständlicher Text für die Oberfläche.</summary>
public sealed record ServiceResult(bool Success, string? Error = null)
{
    public static ServiceResult Ok { get; } = new(true);

    public static ServiceResult Fail(string error) => new(false, error);
}
