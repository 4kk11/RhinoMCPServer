namespace RhinoMCPServer.McpHost.DTOs;

/// <summary>
/// DTO for tool execution result (used for JSON serialization across context boundary)
/// </summary>
public class ToolExecutionResult
{
    public bool IsError { get; set; }
    public List<ContentItem> Contents { get; set; } = new();
}
