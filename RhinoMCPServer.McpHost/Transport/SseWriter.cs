using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using System.Buffers;
using System.Net.ServerSentEvents;
using System.Text.Json;
using System.Threading.Channels;

namespace RhinoMCPServer.McpHost.Transport;

/// <summary>
/// SSE writer for sending JSON-RPC messages as Server-Sent Events.
/// </summary>
internal sealed class SseWriter : IAsyncDisposable
{
    private readonly Channel<SseItem<JsonRpcMessage?>> _messageChannel;
    private Utf8JsonWriter? _jsonWriter;
    private Task? _writeTask;
    private CancellationToken? _writeCancellationToken;
    private readonly SemaphoreSlim _disposeLock = new(1, 1);
    private bool _disposed;

    public SseWriter()
    {
        _messageChannel = Channel.CreateBounded<SseItem<JsonRpcMessage?>>(new BoundedChannelOptions(1)
        {
            SingleReader = true,
            SingleWriter = false,
        });
    }

    /// <summary>
    /// Writes all SSE messages to the response stream until cancellation.
    /// </summary>
    public Task WriteAllAsync(Stream sseResponseStream, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sseResponseStream);

        _writeCancellationToken = cancellationToken;

        var messages = _messageChannel.Reader.ReadAllAsync(cancellationToken);
        _writeTask = SseFormatter.WriteAsync(messages, sseResponseStream, WriteJsonRpcMessageToBuffer, cancellationToken);
        return _writeTask;
    }

    /// <summary>
    /// Sends a JSON-RPC message as an SSE event.
    /// </summary>
    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        await _disposeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            await _messageChannel.Writer.WriteAsync(new SseItem<JsonRpcMessage?>(message, "message"), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _disposeLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _disposeLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            _messageChannel.Writer.Complete();
            try
            {
                if (_writeTask is not null)
                {
                    await _writeTask.ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (_writeCancellationToken?.IsCancellationRequested == true)
            {
                // Ignore exceptions caused by intentional cancellation during shutdown.
            }
            finally
            {
                _jsonWriter?.Dispose();
                _disposed = true;
            }
        }
        finally
        {
            _disposeLock.Release();
        }
    }

    private void WriteJsonRpcMessageToBuffer(SseItem<JsonRpcMessage?> item, IBufferWriter<byte> writer)
    {
        if (item.Data is null)
        {
            return;
        }

        JsonSerializer.Serialize(GetUtf8JsonWriter(writer), item.Data, McpJsonUtilities.DefaultOptions);
    }

    private Utf8JsonWriter GetUtf8JsonWriter(IBufferWriter<byte> writer)
    {
        if (_jsonWriter is null)
        {
            _jsonWriter = new Utf8JsonWriter(writer);
        }
        else
        {
            _jsonWriter.Reset(writer);
        }

        return _jsonWriter;
    }
}
