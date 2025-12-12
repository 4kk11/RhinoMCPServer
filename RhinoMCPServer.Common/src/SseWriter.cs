using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using System.Buffers;
using System.Net.ServerSentEvents;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace RhinoMCPServer.Common;

/// <summary>
/// Writes Server-Sent Events (SSE) to a stream for MCP communication.
/// </summary>
internal sealed class SseWriter : IAsyncDisposable
{
    private readonly string? _messageEndpoint;
    private readonly Channel<SseItem<JsonRpcMessage?>> _messages;
    private Utf8JsonWriter? _jsonWriter;
    private Task? _writeTask;
    private CancellationToken? _writeCancellationToken;
    private readonly SemaphoreSlim _disposeLock = new(1, 1);
    private bool _disposed;

    public SseWriter(string? messageEndpoint = null)
    {
        _messageEndpoint = messageEndpoint;
        _messages = Channel.CreateBounded<SseItem<JsonRpcMessage?>>(new BoundedChannelOptions(1)
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

        // Send initial endpoint event if configured
        if (_messageEndpoint is not null && !_messages.Writer.TryWrite(new SseItem<JsonRpcMessage?>(null, "endpoint")))
        {
            throw new InvalidOperationException("Failed to write initial endpoint event.");
        }

        _writeCancellationToken = cancellationToken;

        var messages = _messages.Reader.ReadAllAsync(cancellationToken);
        _writeTask = SseFormatter.WriteAsync(messages, sseResponseStream, WriteJsonRpcMessageToBuffer, cancellationToken);
        return _writeTask;
    }

    /// <summary>
    /// Sends a JSON-RPC message as an SSE event.
    /// </summary>
    public async Task<bool> SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        await _disposeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return false;
            }

            await _messages.Writer.WriteAsync(new SseItem<JsonRpcMessage?>(message, "message"), cancellationToken).ConfigureAwait(false);
            return true;
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

            _messages.Writer.Complete();
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
        if (item.EventType == "endpoint" && _messageEndpoint is not null)
        {
            writer.Write(Encoding.UTF8.GetBytes(_messageEndpoint));
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
