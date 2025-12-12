using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace RhinoMCPServer.Common
{
    /// <summary>
    /// Adapter that bridges ToolManager to IToolExecutor interface.
    /// Uses standard JsonSerializer for serialization to avoid MCP SDK's source-generated context.
    /// </summary>
    public class ToolExecutorAdapter
    {
        private readonly ToolManager _toolManager;
        private readonly JsonSerializerOptions _jsonOptions;

        public ToolExecutorAdapter(ToolManager toolManager)
        {
            _toolManager = toolManager ?? throw new ArgumentNullException(nameof(toolManager));

            // Use standard JsonSerializer options (NOT McpJsonUtilities.DefaultOptions)
            // This avoids triggering the source-generated JSON context that requires System.Text.Json 10.x
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        /// <summary>
        /// Gets the list of available tools as a JSON string.
        /// </summary>
        public string ListToolsJson()
        {
            var toolsTask = _toolManager.ListToolsAsync();
            var result = toolsTask.AsTask().GetAwaiter().GetResult();

            var toolDefinitions = result.Tools.Select(t => new ToolDefinitionDto
            {
                Name = t.Name,
                Description = t.Description ?? "",
                InputSchemaJson = t.InputSchema.GetRawText()
            }).ToList();

            return JsonSerializer.Serialize(toolDefinitions, _jsonOptions);
        }

        /// <summary>
        /// Executes a tool and returns the result as a JSON string.
        /// </summary>
        public async Task<string> ExecuteToolJsonAsync(string toolName, string argumentsJson)
        {
            // Parse arguments from JSON string to JsonElement dictionary
            var arguments = string.IsNullOrEmpty(argumentsJson) || argumentsJson == "{}"
                ? new Dictionary<string, JsonElement>()
                : JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(argumentsJson, _jsonOptions)
                  ?? new Dictionary<string, JsonElement>();

            // Create CallToolRequestParams
            var request = new CallToolRequestParams
            {
                Name = toolName,
                Arguments = arguments
            };

            try
            {
                var result = await _toolManager.ExecuteToolAsync(request, null);
                return ConvertToJson(result);
            }
            catch (Exception ex)
            {
                var errorResult = new ToolExecutionResultDto
                {
                    IsError = true,
                    Contents = new List<ContentItemDto>
                    {
                        new ContentItemDto { Type = "text", Text = ex.Message }
                    }
                };
                return JsonSerializer.Serialize(errorResult, _jsonOptions);
            }
        }

        private string ConvertToJson(CallToolResult result)
        {
            var dto = new ToolExecutionResultDto
            {
                IsError = result.IsError ?? false,
                Contents = new List<ContentItemDto>()
            };

            if (result.Content != null)
            {
                foreach (var content in result.Content)
                {
                    if (content is TextContentBlock textBlock)
                    {
                        dto.Contents.Add(new ContentItemDto
                        {
                            Type = "text",
                            Text = textBlock.Text
                        });
                    }
                    else if (content is ImageContentBlock imageBlock)
                    {
                        dto.Contents.Add(new ContentItemDto
                        {
                            Type = "image",
                            Data = imageBlock.Data,
                            MimeType = imageBlock.MimeType
                        });
                    }
                }
            }

            return JsonSerializer.Serialize(dto, _jsonOptions);
        }
    }

    /// <summary>
    /// DTO for tool definition (compatible with McpHost.ToolDefinition)
    /// </summary>
    internal class ToolDefinitionDto
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string InputSchemaJson { get; set; } = "{}";
    }

    /// <summary>
    /// DTO for tool execution result (compatible with McpHost.ToolExecutionResult)
    /// </summary>
    internal class ToolExecutionResultDto
    {
        public bool IsError { get; set; }
        public List<ContentItemDto> Contents { get; set; } = new();
    }

    /// <summary>
    /// DTO for content item (compatible with McpHost.ContentItem)
    /// </summary>
    internal class ContentItemDto
    {
        public string Type { get; set; } = "text";
        public string? Text { get; set; }
        public string? Data { get; set; }
        public string? MimeType { get; set; }
    }
}
