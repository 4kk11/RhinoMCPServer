using System.Net;

namespace RhinoMCPServer.McpHost.Transport;

/// <summary>
/// Helper class for setting SSE (Server-Sent Events) response headers.
/// Pure static class following Carmack's principle.
/// </summary>
public static class SseResponseHelper
{
    /// <summary>
    /// Sets the required headers for SSE responses.
    /// </summary>
    /// <param name="context">The HTTP listener context</param>
    /// <param name="sessionId">The MCP session ID</param>
    public static void SetSseResponseHeaders(HttpListenerContext context, string sessionId)
    {
        context.Response.ContentType = "text/event-stream";
        context.Response.Headers.Add("Cache-Control", "no-cache, no-store");
        context.Response.Headers.Add("Connection", "keep-alive");
        context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
        context.Response.Headers.Add("Access-Control-Expose-Headers", "Mcp-Session-Id");
        context.Response.Headers.Add("Mcp-Session-Id", sessionId);
    }
}
