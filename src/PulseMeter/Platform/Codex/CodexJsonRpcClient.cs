using System.Collections.Concurrent;
using System.IO;
using System.Text.Json.Serialization;
using System.Text.Json;

namespace PulseMeter.Platform.Codex;

public interface IJsonRpcClient : IAsyncDisposable
{
    event EventHandler<JsonRpcNotificationEventArgs>? NotificationReceived;

    void Start();

    Task<JsonElement> SendRequestAsync(string method, object? parameters = null, CancellationToken cancellationToken = default);

    Task SendNotificationAsync(string method, object? parameters = null, CancellationToken cancellationToken = default);
}

public interface IJsonRpcClientFactory
{
    IJsonRpcClient Create(StreamReader reader, StreamWriter writer);
}

public sealed class JsonRpcClientFactory : IJsonRpcClientFactory
{
    public IJsonRpcClient Create(StreamReader reader, StreamWriter writer)
    {
        return new CodexJsonRpcClient(reader, writer);
    }
}

public sealed class CodexJsonRpcClient : IJsonRpcClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly CancellationTokenSource _readLoopCts = new();
    private readonly ConcurrentDictionary<int, TaskCompletionSource<JsonElement>> _pending = new();
    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private int _nextId;
    private Task? _readLoop;

    public CodexJsonRpcClient(StreamReader reader, StreamWriter writer)
    {
        _reader = reader;
        _writer = writer;
        _writer.AutoFlush = true;
    }

    public event EventHandler<JsonRpcNotificationEventArgs>? NotificationReceived;

    public void Start()
    {
        _readLoop ??= Task.Run(ReadLoopAsync);
    }

    public async Task<JsonElement> SendRequestAsync(string method, object? parameters = null, CancellationToken cancellationToken = default)
    {
        var id = Interlocked.Increment(ref _nextId);
        var tcs = new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_pending.TryAdd(id, tcs))
        {
            throw new InvalidOperationException("Duplicate JSON-RPC request id.");
        }

        await WriteMessageAsync(new JsonRpcOutboundMessage(method, id, parameters), cancellationToken).ConfigureAwait(false);

        using var registration = cancellationToken.Register(() =>
        {
            if (_pending.TryRemove(id, out var pending))
            {
                pending.TrySetCanceled(cancellationToken);
            }
        });

        return await tcs.Task.ConfigureAwait(false);
    }

    public Task SendNotificationAsync(string method, object? parameters = null, CancellationToken cancellationToken = default)
    {
        return WriteMessageAsync(new JsonRpcOutboundMessage(method, null, parameters), cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        _readLoopCts.Cancel();

        if (_readLoop is not null)
        {
            try
            {
                await _readLoop.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
        }

        _readLoopCts.Dispose();
        _writeLock.Dispose();
    }

    private async Task WriteMessageAsync(JsonRpcOutboundMessage message, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(message, JsonOptions);

        await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await _writer.WriteLineAsync(json.AsMemory(), cancellationToken).ConfigureAwait(false);
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    private async Task ReadLoopAsync()
    {
        while (!_readLoopCts.IsCancellationRequested)
        {
            var line = await _reader.ReadLineAsync(_readLoopCts.Token).ConfigureAwait(false);
            if (line is null)
            {
                FailPending(new IOException("codex app-server closed stdout."));
                return;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            DispatchLine(line);
        }
    }

    private void DispatchLine(string line)
    {
        using var document = JsonDocument.Parse(line);
        var root = document.RootElement;

        if (root.TryGetProperty("id", out var idProperty) && idProperty.TryGetInt32(out var id))
        {
            if (_pending.TryRemove(id, out var pending))
            {
                if (root.TryGetProperty("error", out var error))
                {
                    pending.TrySetException(ReadJsonRpcError(error));
                }
                else if (root.TryGetProperty("result", out var result))
                {
                    pending.TrySetResult(result.Clone());
                }
                else
                {
                    pending.TrySetResult(default);
                }
            }

            return;
        }

        if (root.TryGetProperty("method", out var methodProperty))
        {
            var method = methodProperty.GetString() ?? string.Empty;
            var parameters = root.TryGetProperty("params", out var paramProperty) ? paramProperty.Clone() : default;
            NotificationReceived?.Invoke(this, new JsonRpcNotificationEventArgs(method, parameters));
        }
    }

    private static JsonRpcException ReadJsonRpcError(JsonElement error)
    {
        int? code = null;
        string? message = null;

        if (error.ValueKind == JsonValueKind.Object)
        {
            if (error.TryGetProperty("code", out var codeProperty) && codeProperty.TryGetInt32(out var parsedCode))
            {
                code = parsedCode;
            }

            if (error.TryGetProperty("message", out var messageProperty))
            {
                message = messageProperty.GetString();
            }
        }

        return new JsonRpcException(code, message ?? "Local app-server returned a JSON-RPC error.");
    }

    private void FailPending(Exception exception)
    {
        foreach (var pair in _pending)
        {
            if (_pending.TryRemove(pair.Key, out var pending))
            {
                pending.TrySetException(exception);
            }
        }
    }

    private sealed record JsonRpcOutboundMessage(string Method, int? Id, object? Params)
    {
        public bool ShouldSerializeId() => Id is not null;

        public bool ShouldSerializeParams() => Params is not null;
    }
}

public sealed class JsonRpcNotificationEventArgs : EventArgs
{
    public JsonRpcNotificationEventArgs(string method, JsonElement parameters)
    {
        Method = method;
        Parameters = parameters;
    }

    public string Method { get; }

    public JsonElement Parameters { get; }
}
