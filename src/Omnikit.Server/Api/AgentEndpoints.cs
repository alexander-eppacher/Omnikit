using Microsoft.AspNetCore.Http.HttpResults;
using Omnikit.Server.Data;
using Omnikit.Server.Services;
using Omnikit.Shared;

namespace Omnikit.Server.Api;

/// <summary>
/// Schnittstelle für die Agents (siehe <see cref="AgentApi"/>). Ohne gültigen Token gibt es nur 401 –
/// ohne Hinweis, ob der Token unbekannt oder widerrufen ist.
/// </summary>
public static class AgentEndpoints
{
    public const string RegisterRateLimit = "register";

    public static void MapAgentEndpoints(this WebApplication app)
    {
        app.MapPost(AgentApi.Register, RegisterAsync).RequireRateLimiting(RegisterRateLimit);
        app.MapPost(AgentApi.Report, ReportAsync);
        app.MapGet(AgentApi.Tasks, TasksAsync);
        app.MapPost("/api/agent/tasks/{taskId:int}/result", ResultAsync);
    }

    private static async Task<Results<Ok<RegisterResponse>, UnauthorizedHttpResult>> RegisterAsync(
        RegisterRequest request, RegistrationService registration, CancellationToken cancellationToken)
    {
        var response = await registration.RegisterAsync(request, cancellationToken);
        return response is null ? TypedResults.Unauthorized() : TypedResults.Ok(response);
    }

    private static async Task<Results<NoContent, UnauthorizedHttpResult, BadRequest>> ReportAsync(
        HttpContext context, ReportRequest request, AgentService agents, CancellationToken cancellationToken)
    {
        if (await FindDeviceAsync(context, agents, cancellationToken) is not Device device)
        {
            return TypedResults.Unauthorized();
        }

        if (request.Report is null)
        {
            return TypedResults.BadRequest();
        }

        await agents.SaveReportAsync(device, request, cancellationToken);
        return TypedResults.NoContent();
    }

    private static async Task<Results<Ok<IReadOnlyList<AgentTask>>, UnauthorizedHttpResult>> TasksAsync(
        HttpContext context, AgentService agents, CancellationToken cancellationToken)
    {
        if (await FindDeviceAsync(context, agents, cancellationToken) is not Device device)
        {
            return TypedResults.Unauthorized();
        }

        return TypedResults.Ok(await agents.TakeTasksAsync(device, cancellationToken));
    }

    private static async Task<Results<NoContent, UnauthorizedHttpResult, NotFound>> ResultAsync(
        HttpContext context, int taskId, TaskResultRequest result, AgentService agents, CancellationToken cancellationToken)
    {
        if (await FindDeviceAsync(context, agents, cancellationToken) is not Device device)
        {
            return TypedResults.Unauthorized();
        }

        return await agents.SaveResultAsync(device, taskId, result, cancellationToken) ? TypedResults.NoContent() : TypedResults.NotFound();
    }

    private static Task<Device?> FindDeviceAsync(HttpContext context, AgentService agents, CancellationToken cancellationToken)
    {
        var header = context.Request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        var token = header.StartsWith(prefix, StringComparison.Ordinal) ? header[prefix.Length..].Trim() : null;
        return agents.FindDeviceAsync(token, cancellationToken);
    }
}
