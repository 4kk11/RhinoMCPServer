using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RhinoMCPServer.McpHost.Mcp;

/// <summary>
/// Factory for creating MCP server options.
/// Pure class following Carmack's principle.
/// </summary>
public static class McpServerOptionsFactory
{
    private const string ServerName = "MCPRhinoServer";
    private const string ServerVersion = "1.0.0";
    private const string ProtocolVersion = "2025-03-26";
    private const string ServerInstructions = "This is a Model Context Protocol server for Rhino.";

    /// <summary>
    /// Creates MCP server options with the specified tool handler.
    /// </summary>
    /// <param name="toolHandler">The tool handler for processing tool requests</param>
    /// <returns>Configured MCP server options</returns>
    public static McpServerOptions Create(McpToolHandler toolHandler)
    {
        ArgumentNullException.ThrowIfNull(toolHandler);

        return new McpServerOptions
        {
            ServerInfo = new Implementation { Name = ServerName, Version = ServerVersion },
            Capabilities = new ServerCapabilities
            {
                Tools = new ToolsCapability(),
                Resources = new ResourcesCapability(),
                Prompts = new PromptsCapability(),
            },
            ProtocolVersion = ProtocolVersion,
            ServerInstructions = ServerInstructions,
            Handlers = new McpServerHandlers
            {
                ListToolsHandler = toolHandler.HandleListToolsAsync,
                CallToolHandler = toolHandler.HandleCallToolAsync,
            },
        };
    }
}
