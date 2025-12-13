namespace RhinoMCPServer.McpHost.DTOs;

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
