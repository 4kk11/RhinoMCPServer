namespace RhinoMCPServer.McpHost.DTOs;

/// <summary>
/// DTO for tool definition (used for JSON serialization across context boundary)
/// </summary>
public class ToolDefinition
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string InputSchemaJson { get; set; } = "{}";
}
