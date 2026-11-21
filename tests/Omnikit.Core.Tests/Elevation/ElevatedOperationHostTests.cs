using System.IO.Pipes;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Omnikit.Core.Elevation;

namespace Omnikit.Core.Tests.Elevation;

public class ElevatedOperationHostTests
{
    private sealed class FakeOperation(string id, Func<IReadOnlyList<string>, IProgress<string>, string> run) : IElevatedOperation
    {
        public string Id => id;

        public Task<string> RunAsync(IReadOnlyList<string> arguments, IProgress<string> progress, CancellationToken cancellationToken) =>
            Task.FromResult(run(arguments, progress));
    }

    [Fact]
    public void ValidArgumentsAreAccepted()
    {
        var pipeName = ElevatedOperationHost.CreatePipeName();

        var ok = ElevatedOperationHost.TryParseArguments(
            [ElevatedOperationHost.Argument, "stick.create", pipeName, "3", @"C:\Users\anna\Downloads\Win11.iso"],
            out var operationId, out var parsedPipe, out var arguments);

        Assert.True(ok);
        Assert.Equal("stick.create", operationId);
        Assert.Equal(pipeName, parsedPipe);
        Assert.Equal(["3", @"C:\Users\anna\Downloads\Win11.iso"], arguments);
    }

    [Theory]
    [InlineData("--elevated-operation", "op")]                                  // Pipe fehlt
    [InlineData("--elevated-operation", "op", "irgendein-name")]                // falsches Pipe-Format
    [InlineData("--elevated-operation", "op", "Omnikit-keine-guid")]
    [InlineData("--anderes-argument", "op", "Omnikit-0123456789abcdef0123456789abcdef")]
    [InlineData("--elevated-operation", "", "Omnikit-0123456789abcdef0123456789abcdef")]
    [InlineData("--elevated-operation", "op", "Omnikit-0123456789abcdef0123456789abcdef", "mit\nZeilenumbruch")]
    [InlineData("--elevated-operation", "op", "Omnikit-0123456789abcdef0123456789abcdef", "1", "2", "3", "4", "5", "6", "7", "8", "9")]
    public void InvalidArgumentsAreRejected(params string[] args)
    {
        Assert.False(ElevatedOperationHost.TryParseArguments(args, out _, out _, out _));
    }

    [Fact]
    public void OverlongArgumentIsRejected()
    {
        Assert.False(ElevatedOperationHost.TryParseArguments(
            [ElevatedOperationHost.Argument, "op", ElevatedOperationHost.CreatePipeName(), new string('x', 2000)], out _, out _, out _));
    }

    [Fact]
    public async Task UnknownOperationIsRejected()
    {
        var exitCode = await ElevatedOperationHost.RunAsync(
            [ElevatedOperationHost.Argument, "nicht-in-der-liste", ElevatedOperationHost.CreatePipeName()],
            [new FakeOperation("erlaubt", (_, _) => "{}")],
            NullLogger.Instance);

        Assert.Equal(3, exitCode);
    }

    [Fact]
    public async Task ProgressAndResultAreSentThroughThePipe()
    {
        var (exitCode, lines) = await RunAsync(new FakeOperation("test.op", (arguments, progress) =>
        {
            progress.Report("Schritt 1");
            progress.Report("Schritt 2");
            return $"[{string.Join(",", arguments)}]";
        }), "1", "2");

        Assert.Equal(0, exitCode);
        Assert.Equal(["Schritt 1", "Schritt 2"], lines.SkipLast(1).Select(line => line.Progress));
        Assert.True(lines[^1].Success);
        Assert.Equal("[1,2]", lines[^1].Payload);
    }

    [Fact]
    public async Task FailingOperationIsReportedAsFailure()
    {
        var (exitCode, lines) = await RunAsync(new FakeOperation("test.op", (_, _) => throw new InvalidOperationException("kaputt")));

        Assert.Equal(4, exitCode);
        Assert.False(lines[^1].Success);
        Assert.Null(lines[^1].Payload); // technische Details nur ins Log
    }

    [Fact]
    public async Task ExpectedRefusalCarriesMessageForTheUser()
    {
        var (exitCode, lines) = await RunAsync(new FakeOperation("test.op", (_, _) => throw new ElevatedOperationException("Das ist kein USB-Stick.")));

        Assert.Equal(4, exitCode);
        Assert.False(lines[^1].Success);
        Assert.Equal("Das ist kein USB-Stick.", lines[^1].Payload);
    }

    private static async Task<(int ExitCode, List<ElevatedMessage> Lines)> RunAsync(IElevatedOperation operation, params string[] arguments)
    {
        var pipeName = ElevatedOperationHost.CreatePipeName();
        await using var server = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
        var connect = server.WaitForConnectionAsync(TestContext.Current.CancellationToken);

        var exitCode = await ElevatedOperationHost.RunAsync(
            [ElevatedOperationHost.Argument, operation.Id, pipeName, .. arguments], [operation], NullLogger.Instance);

        await connect;
        using var reader = new StreamReader(server);
        var text = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => JsonSerializer.Deserialize<ElevatedMessage>(line, ElevatedMessage.JsonOptions)!)
            .ToList();
        return (exitCode, lines);
    }
}
