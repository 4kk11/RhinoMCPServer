using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using RhinoMCPServer.McpHost.Logging;
using RhinoMCPServer.McpHost.Routing;
using RhinoMCPServer.McpHost.Session;
using Serilog;
using System.Net;
using System.Text;
using System.Text.Json;

namespace RhinoMCPServer.McpHost;

/// <summary>
/// MCP Server host that runs in an isolated AssemblyLoadContext.
/// Implements Streamable HTTP protocol.
/// Orchestrates session management and request handling.
/// </summary>
public sealed class McpHostRunner : IAsyncDisposable
{
    private readonly IToolExecutor _toolExecutor;
    private readonly ISessionManager _sessionManager;
    private readonly ILoggingConfiguration _loggingConfiguration;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private ILoggerFactory? _loggerFactory;

    public McpHostRunner(IToolExecutor toolExecutor)
        : this(toolExecutor, null, null)
    {
    }

    internal McpHostRunner(
        IToolExecutor toolExecutor,
        ISessionManager? sessionManager = null,
        ILoggingConfiguration? loggingConfiguration = null)
    {
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
        _sessionManager = sessionManager ?? new SessionManager();
        _loggingConfiguration = loggingConfiguration ?? new LoggingConfiguration();
    }

    public async Task RunAsync(int port, string logDir, CancellationToken cancellationToken = default)
    {
        Log.Information("Starting Streamable HTTP MCP server (isolated context)...");

        _loggerFactory = _loggingConfiguration.Configure(logDir);
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();

        Log.Information("Server started on http://localhost:{Port}", port);
        Log.Information("Streamable HTTP endpoint: POST /mcp");

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await HandleRequestAsync(context, _cts.Token).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        Log.Error(ex, "Unhandled exception in request handler");
                    }
                }, _cts.Token);
            }
        }
        catch (HttpListenerException) when (_cts.Token.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (ObjectDisposedException) when (_cts.Token.IsCancellationRequested)
        {
            // Normal shutdown
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Server error");
            throw;
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            var path = context.Request.Url?.AbsolutePath ?? "";
            var method = context.Request.HttpMethod;

            Log.Information("Request: {Method} {Path}", method, path);

            if (method == "OPTIONS")
            {
                HandleCorsPreflightAsync(context);
                return;
            }

            if (path == "/mcp" || path == "/")
            {
                switch (method)
                {
                    case "POST":
                        await HandlePostRequestAsync(context, cancellationToken).ConfigureAwait(false);
                        break;
                    case "GET":
                        await HandleGetRequestAsync(context, cancellationToken).ConfigureAwait(false);
                        break;
                    case "DELETE":
                        await HandleDeleteRequestAsync(context).ConfigureAwait(false);
                        break;
                    default:
                        context.Response.StatusCode = 405;
                        context.Response.Close();
                        break;
                }
            }
            else
            {
                context.Response.StatusCode = 404;
                context.Response.Close();
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling request");
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch (Exception closeEx)
            {
                Log.Warning(closeEx, "Failed to send error response");
            }
        }
    }

    private static void HandleCorsPreflightAsync(HttpListenerContext context)
    {
        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
        context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, Mcp-Session-Id");
        context.Response.Headers.Add("Access-Control-Expose-Headers", "Mcp-Session-Id");
        context.Response.StatusCode = 204;
        context.Response.Close();
    }

    private async Task HandlePostRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
        var body = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

        Log.Debug("Received POST body: {Body}", body);

        var message = JsonSerializer.Deserialize<JsonRpcMessage>(body, McpJsonUtilities.DefaultOptions);

        if (message == null)
        {
            Log.Warning("Invalid message format");
            context.Response.StatusCode = 400;
            await WriteJsonResponseAsync(context, new { error = "Invalid JSON-RPC message" }).ConfigureAwait(false);
            return;
        }

        var sessionId = context.Request.Headers["Mcp-Session-Id"];
        var isInitializeRequest = IsInitializeRequest(message);

        if (isInitializeRequest)
        {
            sessionId = _sessionManager.GenerateSessionId();
            await CreateAndRunSessionAsync(context, message, sessionId, cancellationToken).ConfigureAwait(false);
        }
        else if (string.IsNullOrEmpty(sessionId))
        {
            Log.Warning("Missing Mcp-Session-Id header for non-initialize request");
            context.Response.StatusCode = 400;
            await WriteJsonResponseAsync(context, new { error = "Missing Mcp-Session-Id header" }).ConfigureAwait(false);
        }
        else if (_sessionManager.TryGetSession(sessionId, out var session) && session != null)
        {
            await HandleExistingSessionRequestAsync(context, message, session, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            Log.Warning("Invalid session: {SessionId}", sessionId);
            context.Response.StatusCode = 404;
            await WriteJsonResponseAsync(context, new { error = "Session not found" }).ConfigureAwait(false);
        }
    }

    private static bool IsInitializeRequest(JsonRpcMessage message)
    {
        return message is JsonRpcRequest request && request.Method == "initialize";
    }

    private async Task CreateAndRunSessionAsync(
        HttpListenerContext context,
        JsonRpcMessage initialMessage,
        string sessionId,
        CancellationToken cancellationToken)
    {
        SseResponseHelper.SetSseResponseHeaders(context, sessionId);

        var session = _sessionManager.CreateSession(sessionId, _toolExecutor, _loggerFactory, cancellationToken);
        var responseStream = context.Response.OutputStream;

        try
        {
            await session.Transport.OnMessageReceivedAsync(initialMessage, cancellationToken).ConfigureAwait(false);
            await session.Transport.WritePendingResponsesAsync(responseStream, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Log.Information("Session {SessionId} request cancelled", sessionId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Session {SessionId} error during initial request", sessionId);
        }
        finally
        {
            context.Response.Close();
        }
    }

    private static async Task HandleExistingSessionRequestAsync(
        HttpListenerContext context,
        JsonRpcMessage message,
        SessionInfo session,
        CancellationToken cancellationToken)
    {
        Log.Debug("Handling request for existing session: {SessionId}", session.Transport.SessionId);

        SseResponseHelper.SetSseResponseHeaders(context, session.Transport.SessionId!);

        var responseStream = context.Response.OutputStream;

        try
        {
            await session.Transport.OnMessageReceivedAsync(message, cancellationToken).ConfigureAwait(false);
            await session.Transport.WritePendingResponsesAsync(responseStream, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            Log.Debug("Request cancelled for session {SessionId}", session.Transport.SessionId);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling request for session {SessionId}", session.Transport.SessionId);
        }
        finally
        {
            context.Response.Close();
        }
    }

    private async Task HandleGetRequestAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        var sessionId = context.Request.Headers["Mcp-Session-Id"];

        if (string.IsNullOrEmpty(sessionId))
        {
            context.Response.StatusCode = 400;
            await WriteJsonResponseAsync(context, new { error = "Missing Mcp-Session-Id header" }).ConfigureAwait(false);
            return;
        }

        if (!_sessionManager.TryGetSession(sessionId, out var session) || session == null)
        {
            context.Response.StatusCode = 404;
            await WriteJsonResponseAsync(context, new { error = "Session not found" }).ConfigureAwait(false);
            return;
        }

        Log.Information("GET request for notifications, session: {SessionId}", sessionId);

        SseResponseHelper.SetSseResponseHeaders(context, sessionId);

        try
        {
            await session.Transport.WriteNotificationsAsync(context.Response.OutputStream, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal disconnection
        }
        finally
        {
            context.Response.Close();
        }
    }

    private async Task HandleDeleteRequestAsync(HttpListenerContext context)
    {
        var sessionId = context.Request.Headers["Mcp-Session-Id"];

        if (string.IsNullOrEmpty(sessionId))
        {
            context.Response.StatusCode = 400;
            await WriteJsonResponseAsync(context, new { error = "Missing Mcp-Session-Id header" }).ConfigureAwait(false);
            return;
        }

        if (await _sessionManager.RemoveSessionAsync(sessionId).ConfigureAwait(false))
        {
            context.Response.StatusCode = 204;
        }
        else
        {
            context.Response.StatusCode = 404;
            await WriteJsonResponseAsync(context, new { error = "Session not found" }).ConfigureAwait(false);
            return;
        }

        context.Response.Close();
    }

    private static async Task WriteJsonResponseAsync(HttpListenerContext context, object response)
    {
        context.Response.ContentType = "application/json";
        var json = JsonSerializer.Serialize(response, McpJsonUtilities.DefaultOptions);
        var bytes = Encoding.UTF8.GetBytes(json);
        await context.Response.OutputStream.WriteAsync(bytes).ConfigureAwait(false);
        context.Response.Close();
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        _listener?.Stop();
        _listener?.Close();
        _listener = null;

        await _sessionManager.DisposeAsync().ConfigureAwait(false);

        _cts?.Dispose();
        _cts = null;

        Log.Information("Server stopped.");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync().ConfigureAwait(false);
        _loggerFactory?.Dispose();
    }
}
