using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RhinoMCPServer.McpHost.DTOs;
using System.Text.Json;

namespace RhinoMCPServer.McpHost.Mcp;

/// <summary>
/// Handles MCP tool-related requests (ListTools, CallTool).
/// </summary>
public class McpToolHandler
{
    private readonly IToolExecutor _toolExecutor;

    public McpToolHandler(IToolExecutor toolExecutor)
    {
        _toolExecutor = toolExecutor ?? throw new ArgumentNullException(nameof(toolExecutor));
    }

    /// <summary>
    /// Handles the ListTools request.
    /// </summary>
    public ValueTask<ListToolsResult> HandleListToolsAsync(
        RequestContext<ListToolsRequestParams> context,
        CancellationToken cancellationToken)
    {
        var toolsJson = _toolExecutor.ListToolsJson();
        var tools = JsonSerializer.Deserialize<List<ToolDefinition>>(toolsJson, McpJsonUtilities.DefaultOptions)
            ?? new List<ToolDefinition>();

        var mcpTools = tools.Select(t => new Tool
        {
            Name = t.Name,
            Description = t.Description,
            InputSchema = JsonSerializer.Deserialize<JsonElement>(t.InputSchemaJson, McpJsonUtilities.DefaultOptions)
        }).ToList();

        return ValueTask.FromResult(new ListToolsResult { Tools = mcpTools });
    }

    /// <summary>
    /// Handles the CallTool request.
    /// </summary>
    public async ValueTask<CallToolResult> HandleCallToolAsync(
        RequestContext<CallToolRequestParams> context,
        CancellationToken cancellationToken)
    {
        var toolName = context.Params?.Name
            ?? throw new McpProtocolException("Missing tool name", McpErrorCode.InvalidParams);

        var argumentsJson = context.Params?.Arguments != null
            ? JsonSerializer.Serialize(context.Params.Arguments, McpJsonUtilities.DefaultOptions)
            : "{}";

        var resultJson = await _toolExecutor.ExecuteToolJsonAsync(toolName, argumentsJson);
        var result = JsonSerializer.Deserialize<ToolExecutionResult>(resultJson, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

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
}
