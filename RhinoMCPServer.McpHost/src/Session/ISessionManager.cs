using Microsoft.Extensions.Logging;

namespace RhinoMCPServer.McpHost.Session;

/// <summary>
/// Interface for managing MCP sessions.
/// Follows Martin's Dependency Inversion principle.
/// </summary>
internal interface ISessionManager : IAsyncDisposable
{
    /// <summary>
    /// Gets a session by ID.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <returns>The session info if found, null otherwise</returns>
    SessionInfo? GetSession(string sessionId);

    /// <summary>
    /// Tries to get a session by ID.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="session">The session info if found</param>
    /// <returns>True if session was found, false otherwise</returns>
    bool TryGetSession(string sessionId, out SessionInfo? session);

    /// <summary>
    /// Creates a new session.
    /// </summary>
    /// <param name="sessionId">The session ID</param>
    /// <param name="toolExecutor">The tool executor</param>
    /// <param name="loggerFactory">Optional logger factory</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created session info</returns>
    SessionInfo CreateSession(
        string sessionId,
        IToolExecutor toolExecutor,
        ILoggerFactory? loggerFactory,
        CancellationToken cancellationToken);

    /// <summary>
    /// Removes and disposes a session.
    /// </summary>
    /// <param name="sessionId">The session ID to remove</param>
    /// <returns>True if session was removed, false if not found</returns>
    Task<bool> RemoveSessionAsync(string sessionId);

    /// <summary>
    /// Generates a unique session ID.
    /// </summary>
    /// <returns>A unique session ID</returns>
    string GenerateSessionId();
}
