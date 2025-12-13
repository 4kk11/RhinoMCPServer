using ModelContextProtocol.Server;

namespace RhinoMCPServer.McpHost.Session;

/// <summary>
/// Represents an active MCP session.
/// </summary>
/// <param name="Transport">The HTTP transport for this session</param>
/// <param name="Server">The MCP server instance</param>
/// <param name="RunTask">The running task for the server</param>
/// <param name="SessionCts">Cancellation token source for this session</param>
internal record SessionInfo(
    StreamableHttpTransport Transport,
    McpServer Server,
    Task RunTask,
    CancellationTokenSource SessionCts);
