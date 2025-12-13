using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using RhinoMCPServer.McpHost.Mcp;
using Serilog;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace RhinoMCPServer.McpHost.Session;

/// <summary>
/// Manages MCP session lifecycle.
/// Single responsibility: session CRUD operations.
/// </summary>
internal sealed class SessionManager : ISessionManager
{
    private readonly ConcurrentDictionary<string, SessionInfo> _sessions = new();

    /// <inheritdoc />
    public SessionInfo? GetSession(string sessionId) =>
        _sessions.TryGetValue(sessionId, out var session) ? session : null;

    /// <inheritdoc />
    public bool TryGetSession(string sessionId, out SessionInfo? session) =>
        _sessions.TryGetValue(sessionId, out session);

    /// <inheritdoc />
    public SessionInfo CreateSession(
        string sessionId,
        IToolExecutor toolExecutor,
        ILoggerFactory? loggerFactory,
        CancellationToken cancellationToken)
    {
        Log.Information("Creating new session: {SessionId}", sessionId);

        var transport = new StreamableHttpTransport(sessionId);
        var toolHandler = new McpToolHandler(toolExecutor);
        var serverOptions = McpServerOptionsFactory.Create(toolHandler);
        var mcpServer = McpServer.Create(transport, serverOptions, loggerFactory);
        var sessionCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var runTask = mcpServer.RunAsync(sessionCts.Token);

        var session = new SessionInfo(transport, mcpServer, runTask, sessionCts);
        _sessions[sessionId] = session;

        return session;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveSessionAsync(string sessionId)
    {
        if (!_sessions.TryRemove(sessionId, out var session))
        {
            return false;
        }

        Log.Information("Terminating session: {SessionId}", sessionId);
        session.SessionCts.Cancel();
        await session.Transport.DisposeAsync().ConfigureAwait(false);
        await session.Server.DisposeAsync().ConfigureAwait(false);

        return true;
    }

    /// <inheritdoc />
    public string GenerateSessionId()
    {
        Span<byte> buffer = stackalloc byte[16];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var sessionId in _sessions.Keys.ToList())
        {
            await RemoveSessionAsync(sessionId).ConfigureAwait(false);
        }
    }
}
