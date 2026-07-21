using System.Text;
using System.Text.Json;
using PulseMeter.Platform.Codex;

namespace PulseMeter.Tests;

public sealed class CodexJsonRpcClientTests
{
    [Fact]
    public async Task MalformedInput_FailsPendingRequestAndClearsPendingCount()
    {
        await using var client = CreateClient("{ malformed json\n", new MemoryStream());
        var request = client.SendRequestAsync("test/request");

        await WaitForPendingRequestAsync(client);
        client.Start();

        await Assert.ThrowsAnyAsync<JsonException>(() => request);
        Assert.Equal(0, client.PendingRequestCount);
    }

    [Fact]
    public async Task EndOfInput_FailsPendingRequestAndClearsPendingCount()
    {
        await using var client = CreateClient(string.Empty, new MemoryStream());
        var request = client.SendRequestAsync("test/request");

        await WaitForPendingRequestAsync(client);
        client.Start();

        await Assert.ThrowsAsync<IOException>(() => request);
        Assert.Equal(0, client.PendingRequestCount);
    }

    [Fact]
    public async Task WriteFailure_RemovesPendingRequest()
    {
        await using var client = CreateClient(string.Empty, new FailingWriteStream());

        await Assert.ThrowsAsync<IOException>(() => client.SendRequestAsync("test/request"));

        Assert.Equal(0, client.PendingRequestCount);
    }

    [Fact]
    public async Task DisposeAsync_FailsPendingRequestImmediately()
    {
        await using var client = CreateClient(string.Empty, new MemoryStream());
        var request = client.SendRequestAsync("test/request");

        await WaitForPendingRequestAsync(client);
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => request);
        Assert.Equal(0, client.PendingRequestCount);
    }

    [Fact]
    public async Task DisposeAsync_IsIdempotentAndRejectsNewMessages()
    {
        await using var client = CreateClient(string.Empty, new MemoryStream());

        await client.DisposeAsync();
        await client.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SendRequestAsync("test/request"));
        await Assert.ThrowsAsync<ObjectDisposedException>(() => client.SendNotificationAsync("test/notification"));
    }

    [Fact]
    public async Task DisposeAsync_WaitsForInFlightWriteAndCleansItsPendingRequest()
    {
        var output = new BlockingWriteStream();
        var client = CreateClient(string.Empty, output);
        try
        {
            var request = client.SendRequestAsync("test/request");
            await output.WriteStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var disposal = client.DisposeAsync().AsTask();

            Assert.False(disposal.IsCompleted);
            Assert.Equal(0, client.PendingRequestCount);

            output.ReleaseWrite();
            await disposal;
            await Assert.ThrowsAsync<ObjectDisposedException>(() => request);
        }
        finally
        {
            output.ReleaseWrite();
            await client.DisposeAsync();
        }
    }

    [Fact]
    public async Task ConcurrentStartAndDispose_EitherStartsBeforeDisposalOrThrowsObjectDisposed()
    {
        await using var client = CreateClient(string.Empty, new MemoryStream());
        using var barrier = new Barrier(3);

        var startAttempt = Task.Run(() =>
        {
            barrier.SignalAndWait();
            try
            {
                client.Start();
                return (Exception?)null;
            }
            catch (Exception exception)
            {
                return exception;
            }
        });
        var disposeAttempt = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            await client.DisposeAsync();
        });

        barrier.SignalAndWait();
        var startException = await startAttempt;
        await disposeAttempt;

        Assert.True(startException is null or ObjectDisposedException);
        Assert.Throws<ObjectDisposedException>(() => client.Start());
    }

    [Fact]
    public async Task ConcurrentDisposeAsyncCalls_ShareOneCompletionTask()
    {
        await using var client = CreateClient(string.Empty, new MemoryStream());
        using var barrier = new Barrier(3);
        Task? firstDisposeTask = null;
        Task? secondDisposeTask = null;

        var firstCaller = Task.Run(() =>
        {
            barrier.SignalAndWait();
            firstDisposeTask = client.DisposeAsync().AsTask();
        });
        var secondCaller = Task.Run(() =>
        {
            barrier.SignalAndWait();
            secondDisposeTask = client.DisposeAsync().AsTask();
        });

        barrier.SignalAndWait();
        await Task.WhenAll(firstCaller, secondCaller);

        Assert.NotNull(firstDisposeTask);
        Assert.NotNull(secondDisposeTask);
        Assert.Same(firstDisposeTask, secondDisposeTask);
        await firstDisposeTask;
    }

    [Fact]
    public async Task RequestCancellationAfterPendingRegistration_RemovesPendingAndPreservesToken()
    {
        await using var client = CreateClient(string.Empty, new MemoryStream());
        using var cancellation = new CancellationTokenSource();
        var request = client.SendRequestAsync("test/request", cancellationToken: cancellation.Token);

        await WaitForPendingRequestAsync(client);
        cancellation.Cancel();

        var exception = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, client.PendingRequestCount);
    }

    private static CodexJsonRpcClient CreateClient(string input, Stream output)
    {
        var inputBytes = Encoding.UTF8.GetBytes(input);
        var reader = new StreamReader(new MemoryStream(inputBytes), Encoding.UTF8);
        var writer = new StreamWriter(output, Encoding.UTF8, leaveOpen: true);
        return new CodexJsonRpcClient(reader, writer);
    }

    private static async Task WaitForPendingRequestAsync(CodexJsonRpcClient client)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (client.PendingRequestCount == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(10), timeout.Token);
        }

        Assert.Equal(1, client.PendingRequestCount);
    }

    private sealed class FailingWriteStream : MemoryStream
    {
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            return Task.FromException(new IOException("Simulated write failure."));
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException(new IOException("Simulated write failure."));
        }
    }

    private sealed class BlockingWriteStream : MemoryStream
    {
        public TaskCompletionSource WriteStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private TaskCompletionSource WriteCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WriteStarted.TrySetResult();
            await WriteCompletion.Task.WaitAsync(cancellationToken);
        }

        public void ReleaseWrite()
        {
            WriteCompletion.TrySetResult();
        }
    }
}
