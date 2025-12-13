using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;

namespace RhinoMCPServer.McpHost;

/// <summary>
/// MCP Server host that runs in an isolated AssemblyLoadContext.
/// Implements Streamable HTTP protocol.
/// </summary>
public sealed class McpHostRunner
{
    private readonly IToolExecutor _toolExecutor;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();
    private ILoggerFactory? _loggerFactory;

    private record SessionInfo(
        StreamableHttpTransport Transport,
        McpServer Server,
        Task RunTask,
        CancellationTokenSource SessionCts);

    public McpHostRunner(IToolExecutor toolExecutor)
    {
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
    }

    private void ConfigureLogging(string logDir)
    {
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(Path.Combine(logDir, "MCPRhinoServer_.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        _loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddSerilog();
        });
    }

    public async Task RunAsync(int port, string logDir, CancellationToken cancellationToken = default)
    {
        Console.WriteLine("Starting Streamable HTTP MCP server (isolated context)...");

        ConfigureLogging(logDir);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        _listener = new HttpListener();
        _listener.Prefixes.Add($"http://localhost:{port}/");
        _listener.Start();

        Console.WriteLine($"Server started on http://localhost:{port}");
        Console.WriteLine("Streamable HTTP endpoint: POST /mcp");

        try
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync().ConfigureAwait(false);
                _ = HandleRequestAsync(context, _cts.Token);
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
            Console.WriteLine($"Server error: {ex.Message}");
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

            // Handle CORS preflight
            if (method == "OPTIONS")
            {
                await HandleCorsPreflightAsync(context).ConfigureAwait(false);
                return;
            }

            // Streamable HTTP endpoint
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
            catch { }
        }
    }

    private Task HandleCorsPreflightAsync(HttpListenerContext context)
    {
        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, DELETE, OPTIONS");
        context.Response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Accept, Mcp-Session-Id");
        context.Response.Headers.Add("Access-Control-Expose-Headers", "Mcp-Session-Id");
        context.Response.StatusCode = 204;
        context.Response.Close();
        return Task.CompletedTask;
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
            sessionId = GenerateSessionId();
            await CreateAndRunSessionAsync(context, message, sessionId, cancellationToken).ConfigureAwait(false);
        }
        else if (string.IsNullOrEmpty(sessionId))
        {
            Log.Warning("Missing Mcp-Session-Id header for non-initialize request");
            context.Response.StatusCode = 400;
            await WriteJsonResponseAsync(context, new { error = "Missing Mcp-Session-Id header" }).ConfigureAwait(false);
        }
        else if (_sessions.TryGetValue(sessionId, out var session))
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
        if (message is JsonRpcRequest request)
        {
            return request.Method == "initialize";
        }
        return false;
    }

    private async Task CreateAndRunSessionAsync(
        HttpListenerContext context,
        JsonRpcMessage initialMessage,
        string sessionId,
        CancellationToken cancellationToken)
    {
        Log.Information("Creating new session: {SessionId}", sessionId);

        SetSseResponseHeaders(context, sessionId);

        var responseStream = context.Response.OutputStream;
        var transport = new StreamableHttpTransport(sessionId);
        var serverOptions = CreateServerOptions();
        var mcpServer = McpServer.Create(transport, serverOptions, _loggerFactory);
        var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var runTask = mcpServer.RunAsync(sessionCts.Token);

        _sessions[sessionId] = new SessionInfo(transport, mcpServer, runTask, sessionCts);

        try
        {
            await transport.OnMessageReceivedAsync(initialMessage, cancellationToken).ConfigureAwait(false);
            await transport.WritePendingResponsesAsync(responseStream, cancellationToken).ConfigureAwait(false);
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

    private async Task HandleExistingSessionRequestAsync(
        HttpListenerContext context,
        JsonRpcMessage message,
        SessionInfo session,
        CancellationToken cancellationToken)
    {
        Log.Debug("Handling request for existing session: {SessionId}", session.Transport.SessionId);

        SetSseResponseHeaders(context, session.Transport.SessionId!);

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

        if (!_sessions.TryGetValue(sessionId, out var session))
        {
            context.Response.StatusCode = 404;
            await WriteJsonResponseAsync(context, new { error = "Session not found" }).ConfigureAwait(false);
            return;
        }

        Log.Information("GET request for notifications, session: {SessionId}", sessionId);

        SetSseResponseHeaders(context, sessionId);

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

        if (_sessions.TryRemove(sessionId, out var session))
        {
            Log.Information("Terminating session: {SessionId}", sessionId);
            session.SessionCts.Cancel();
            await session.Transport.DisposeAsync().ConfigureAwait(false);
            await session.Server.DisposeAsync().ConfigureAwait(false);
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

    private static void SetSseResponseHeaders(HttpListenerContext context, string sessionId)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.Add("Cache-Control", "no-cache, no-store");
        context.Response.Headers.Add("Connection", "keep-alive");
        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Add("Access-Control-Expose-Headers", "Mcp-Session-Id");
        context.Response.Headers.Add("Mcp-Session-Id", sessionId);
    }

    private McpServerOptions CreateServerOptions()
    {
        return new McpServerOptions
        {
            ServerInfo = new Implementation { Name = "MCPRhinoServer", Version = "1.0.0" },
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability(),
                Resources = new ResourcesCapability(),
                Prompts = new PromptsCapability(),
            },
            ProtocolVersion = "2024-11-05",
            ServerInstructions = "This is a Model Context Protocol server for Rhino.",
            Handlers = new McpServerHandlers
            {
                ListToolsHandler = ListToolsHandler,
                CallToolHandler = CallToolHandler,
            },
        };
    }

    private ValueTask<ListToolsResult> ListToolsHandler(
        RequestContext<ListToolsRequestParams> context,
        CancellationToken cancellationToken)
    {
        var toolsJson = _toolExecutor.ListToolsJson();
        var tools = JsonSerializer.Deserialize<List<ToolDefinition>>(toolsJson, McpJsonUtilities.DefaultOptions) ?? new List<ToolDefinition>();

        var mcpTools = tools.Select(t => new Tool
        {
            Name = t.Name,
            Description = t.Description,
            InputSchema = JsonSerializer.Deserialize<JsonElement>(t.InputSchemaJson, McpJsonUtilities.DefaultOptions)
        }).ToList();

        return ValueTask.FromResult(new ListToolsResult { Tools = mcpTools });
    }

    private async ValueTask<CallToolResult> CallToolHandler(
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken)
    {
        var toolName = context.Params?.Name ?? throw new McpProtocolException("Missing tool name", McpErrorCode.InvalidParams);
        var argumentsJson = context.Params?.Arguments != null
            ? JsonSerializer.Serialize(context.Params.Arguments, McpJsonUtilities.DefaultOptions)
            : "{}";

        var resultJson = await _toolExecutor.ExecuteToolJsonAsync(toolName, argumentsJson);
        var result = JsonSerializer.Deserialize<ToolExecutionResult>(resultJson);

        if (result == null)
        {
            return new CallToolResult
            {
                IsError = true,
                Content = new List<ContentBlock> { new TextContentBlock { Text = "Failed to parse tool result" } }
            };
        }

        var contents = new List<ContentBlock>();
        foreach (var content in result.Contents)
        {
            if (content.Type == "text")
            {
                contents.Add(new TextContentBlock { Text = content.Text ?? "" });
            }
            else if (content.Type == "image")
            {
                contents.Add(new ImageContentBlock { Data = content.Data ?? "", MimeType = content.MimeType ?? "image/png" });
            }
        }

        return new CallToolResult
        {
            IsError = result.IsError,
            Content = contents
        };
    }

    private static string GenerateSessionId()
    {
        Span<byte> buffer = stackalloc byte[16];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    private async Task WriteJsonResponseAsync(HttpListenerContext context, object response)
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

        foreach (var session in _sessions.Values)
        {
            session.SessionCts.Cancel();
            await session.Transport.DisposeAsync().ConfigureAwait(false);
            await session.Server.DisposeAsync().ConfigureAwait(false);
        }
        _sessions.Clear();

        _cts?.Dispose();
        _cts = null;

        Console.WriteLine("Server stopped.");
    }
}

/// <summary>
/// DTO for tool definition (used for JSON serialization across context boundary)
/// </summary>
public class ToolDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string InputSchemaJson { get; set; } = "{}";
}

/// <summary>
/// DTO for tool execution result (used for JSON serialization across context boundary)
/// </summary>
public class ToolExecutionResult
{
    public bool IsError { get; set; }
    public List<ContentItem> Contents { get; set; } = new();
}

/// <summary>
/// DTO for content item
/// </summary>
public class ContentItem
{
    public string Type { get; set; } = "text";
    public string? Text { get; set; }
    public string? Data { get; set; }
    public string? MimeType { get; set; }
}
