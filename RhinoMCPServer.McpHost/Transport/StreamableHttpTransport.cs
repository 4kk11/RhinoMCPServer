using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System.Threading.Channels;

namespace RhinoMCPServer.McpHost.Transport;

/// <summary>
/// Custom ITransport implementation for Streamable HTTP communication.
/// Each POST request gets responses via SSE stream in the response body.
/// </summary>
internal sealed class StreamableHttpTransport : ITransport
{
    private readonly Channel<JsonRpcMessage> _incomingChannel;
    private readonly Channel<JsonRpcMessage> _outgoingChannel;
    private readonly Channel<JsonRpcMessage> _notificationChannel;
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
        _outgoingChannel = Channel.CreateBounded<JsonRpcMessage>(new BoundedChannelOptions(16)
        {
            SingleReader = true,
            SingleWriter = true,
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
    /// </summary>
    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        if (_isDisposed)
        {
            return;
        }

        // Route notifications to notification channel, responses to outgoing channel
        if (message is JsonRpcNotification)
        {
            await _notificationChannel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await _outgoingChannel.Writer.WriteAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Writes pending responses to the HTTP response stream as SSE.
    /// Called after processing a POST request.
    /// </summary>
    public async Task WritePendingResponsesAsync(Stream outputStream, CancellationToken cancellationToken)
    {
        await using var sseWriter = new SseWriter();

        // Start writing SSE in background
        var writeTask = sseWriter.WriteAllAsync(outputStream, cancellationToken);

        // Wait for response(s) and send them
        try
        {
            // Read one response for the request
            if (await _outgoingChannel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (_outgoingChannel.Reader.TryRead(out var response))
                {
                    await sseWriter.SendMessageAsync(response, cancellationToken).ConfigureAwait(false);
                }
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
        _outgoingChannel.Writer.TryComplete();
        _notificationChannel.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }
}
