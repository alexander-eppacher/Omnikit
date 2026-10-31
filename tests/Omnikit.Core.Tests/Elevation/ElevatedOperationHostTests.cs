using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Omnikit.Core.Elevation;

namespace Omnikit.Core.Tests.Elevation;

public class ElevatedOperationHostTests
{
    private sealed class FakeOperation(string id, Func<string> run) : IElevatedOperation
    {
        public string Id => id;
        public Task<string> RunAsync(CancellationToken cancellationToken) => Task.FromResult(run());
    }

    [Fact]
    public void ValidArgumentsAreAccepted()
    {
        var pipeName = ElevatedOperationHost.CreatePipeName();

        var ok = ElevatedOperationHost.TryParseArguments(
            [ElevatedOperationHost.Argument, "health.disk-reliability", pipeName], out var operationId, out var parsedPipe);

        Assert.True(ok);
        Assert.Equal("health.disk-reliability", operationId);
        Assert.Equal(pipeName, parsedPipe);
    }

    [Theory]
    [InlineData("--elevated-operation", "op")]                                  // Pipe fehlt
    [InlineData("--elevated-operation", "op", "irgendein-name")]                // falsches Pipe-Format
    [InlineData("--elevated-operation", "op", "Omnikit-keine-guid")]
    [InlineData("--anderes-argument", "op", "Omnikit-0123456789abcdef0123456789abcdef")]
    [InlineData("--elevated-operation", "", "Omnikit-0123456789abcdef0123456789abcdef")]
    [InlineData("--elevated-operation", "op", "Omnikit-0123456789abcdef0123456789abcdef", "extra")]
    public void InvalidArgumentsAreRejected(params string[] args)
    {
        Assert.False(ElevatedOperationHost.TryParseArguments(args, out _, out _));
    }

    [Fact]
    public async Task UnknownOperationIsRejected()
    {
        var exitCode = await ElevatedOperationHost.RunAsync(
            [ElevatedOperationHost.Argument, "nicht-in-der-liste", ElevatedOperationHost.CreatePipeName()],
            [new FakeOperation("erlaubt", () => "{}")],
            NullLogger.Instance);

        Assert.Equal(3, exitCode);
    }

    [Fact]
    public async Task ResultIsSentThroughThePipe()
    {
        var pipeName = ElevatedOperationHost.CreatePipeName();
        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var connect = server.WaitForConnectionAsync(TestContext.Current.CancellationToken);

        var exitCode = await ElevatedOperationHost.RunAsync(
            [ElevatedOperationHost.Argument, "test.op", pipeName],
            [new FakeOperation("test.op", () => "[1,2,3]")],
            NullLogger.Instance);

        await connect;
        using var reader = new StreamReader(server);
        var response = JsonSerializer.Deserialize<ElevatedResponse>(
            await reader.ReadToEndAsync(TestContext.Current.CancellationToken), ElevatedResponse.JsonOptions);

        Assert.Equal(0, exitCode);
        Assert.NotNull(response);
        Assert.True(response.Success);
        Assert.Equal("[1,2,3]", response.Payload);
    }

    [Fact]
    public async Task FailingOperationIsReportedAsFailure()
    {
        var pipeName = ElevatedOperationHost.CreatePipeName();
        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var connect = server.WaitForConnectionAsync(TestContext.Current.CancellationToken);

        var exitCode = await ElevatedOperationHost.RunAsync(
            [ElevatedOperationHost.Argument, "test.op", pipeName],
            [new FakeOperation("test.op", () => throw new InvalidOperationException("kaputt"))],
            NullLogger.Instance);

        await connect;
        using var reader = new StreamReader(server);
        var response = JsonSerializer.Deserialize<ElevatedResponse>(
            await reader.ReadToEndAsync(TestContext.Current.CancellationToken), ElevatedResponse.JsonOptions);

        Assert.Equal(4, exitCode);
        Assert.False(response!.Success);
    }
}
