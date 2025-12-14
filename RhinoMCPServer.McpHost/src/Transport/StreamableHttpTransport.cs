using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace RhinoMCPServer.McpHost.Transport;

/// <summary>
/// Custom ITransport implementation for Streamable HTTP communication.
/// Each POST request gets responses via SSE stream in the response body.
/// Supports concurrent request handling by matching responses to request IDs.
/// </summary>
internal sealed class StreamableHttpTransport : ITransport
{
    private readonly Channel<JsonRpcMessage> _incomingChannel;
    private readonly Channel<JsonRpcMessage> _notificationChannel;
    private readonly ConcurrentDictionary<RequestId, TaskCompletionSource<JsonRpcMessage>> _pendingRequests = new();
    private bool _isDisposed;

    public string? SessionId { get; }
    public ChannelReader<JsonRpcMessage> MessageReader => _incomingChannel.Reader;

    public StreamableHttpTransport(string sessionId)
    {
        SessionId = sessionId;
        _incomingChannel = Channel.CreateBounded<JsonRpcMessage>(new BoundedChannelOptions(16)
        {
            SingleReader = true,
            SingleWriter = false,
        });
        _notificationChannel = Channel.CreateUnbounded<JsonRpcMessage>(new UnboundedChannelOptions
        {
            SingleReader = false,
            SingleWriter = true,
        });
    }

    /// <summary>
    /// Called when a JSON-RPC message is received from the client.
    /// </summary>
    public async Task OnMessageReceivedAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            throw new ObjectDisposedException(nameof(StreamableHttpTransport));
        }

        await _incomingChannel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Called by McpServer to send a message back to the client.
    /// Routes responses to matching pending requests, notifications to notification channel.
    /// </summary>
    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            return;
        }

        // Route notifications to notification channel
        if (message is JsonRpcNotification)
        {
            await _notificationChannel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
            return;
        }

        // Route responses to matching pending request
        if (message is JsonRpcResponse response)
        {
            if (_pendingRequests.TryRemove(response.Id, out var tcs))
            {
                tcs.TrySetResult(message);
            }
        }
        else if (message is JsonRpcError error)
        {
            if (_pendingRequests.TryRemove(error.Id, out var tcs))
            {
                tcs.TrySetResult(message);
            }
        }
    }

    /// <summary>
    /// Gets the pending response for a specific request ID.
    /// Used for JSON format responses in Streamable HTTP.
    /// Supports concurrent requests by matching responses to request IDs.
    /// </summary>
    public async Task<JsonRpcMessage?> GetPendingResponseAsync(RequestId requestId, CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<JsonRpcMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingRequests[requestId] = tcs;

        try
        {
            using var registration = cancellationToken.Register(() =>
            {
                if (_pendingRequests.TryRemove(requestId, out var removed))
                {
                    removed.TrySetCanceled(cancellationToken);
                }
            });

            return await tcs.Task.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            _pendingRequests.TryRemove(requestId, out _);
            return null;
        }
    }

    /// <summary>
    /// Writes pending response to the HTTP response stream as SSE.
    /// Called after processing a POST request when SSE format is needed.
    /// </summary>
    public async Task WritePendingResponsesAsync(Stream outputStream, RequestId requestId, CancellationToken cancellationToken)
    {
        await using var sseWriter = new SseWriter();

        // Start writing SSE in background
        var writeTask = sseWriter.WriteAllAsync(outputStream, cancellationToken);

        try
        {
            var response = await GetPendingResponseAsync(requestId, cancellationToken).ConfigureAwait(false);
            if (response != null)
            {
                await sseWriter.SendMessageAsync(response, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Request cancelled
        }
    }

    /// <summary>
    /// Writes server-initiated notifications to a long-lived SSE connection.
    /// Used for GET requests to receive async notifications.
    /// </summary>
    public async Task WriteNotificationsAsync(Stream outputStream, CancellationToken cancellationToken)
    {
        await using var sseWriter = new SseWriter();

        // Start writing SSE
        var writeTask = sseWriter.WriteAllAsync(outputStream, cancellationToken);

        try
        {
            await foreach (var notification in _notificationChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
            {
                await sseWriter.SendMessageAsync(notification, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            // Normal disconnection
        }
    }

    public ValueTask DisposeAsync()
    {
        _isDisposed = true;
        _incomingChannel.Writer.TryComplete();
        _notificationChannel.Writer.TryComplete();

        // Cancel all pending requests
        foreach (var kvp in _pendingRequests)
        {
            if (_pendingRequests.TryRemove(kvp.Key, out var tcs))
            {
                tcs.TrySetCanceled();
            }
        }

        return ValueTask.CompletedTask;
    }
}
