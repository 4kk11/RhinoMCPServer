using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Grasshopper;
using Grasshopper.Kernel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Grasshopper
{
    public class GetCanvasComponentsTool : IMCPTool
    {
        public string Name => "get_canvas_components";
        public string Description => """
            Retrieves information about all components on the Grasshopper canvas, including their GUIDs,
            descriptions, exposed parameters, and other metadata.
            """;

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "include_params": {
                        "type": "boolean",
                        "description": "Whether to include parameter information for each component",
                        "default": false
                    }
                }
            }
            """);

        public Task<CallToolResult> ExecuteAsync(CallToolRequestParams request, McpServer? server)
        {
            try
            {
                bool includeParams = false;
                if (request.Arguments != null && 
                    request.Arguments.TryGetValue("include_params", out var includeParamsValue) && 
                    includeParamsValue is JsonElement includeParamsElement)
                {
                    includeParams = includeParamsElement.GetBoolean();
                }

                // Get active Grasshopper document
                GH_Document? doc = Instances.ActiveCanvas?.Document;
                if (doc == null)
                {
                    throw new McpProtocolException("No active Grasshopper document found");
                }

                // Get all components from the canvas
                var components = doc.Objects
                    .OfType<IGH_DocumentObject>()
                    .Select(obj => new
                    {
                        guid = obj.InstanceGuid.ToString(),
                        name = obj.Name,
                        nickname = obj.NickName,
                        description = obj.Description,
                        category = obj.Category,
                        subcategory = obj.SubCategory,
                        position = new 
                        {
                            x = obj.Attributes.Pivot.X,
                            y = obj.Attributes.Pivot.Y
                        },
                        parameters = includeParams && obj is IGH_Component comp ? new
                        {
                            input = comp.Params.Input.Select(p => new
                            {
                                name = p.Name,
                                nickname = p.NickName,
                                description = p.Description,
                                type_name = p.TypeName
                            }).ToArray(),
                            output = comp.Params.Output.Select(p => new
                            {
                                name = p.Name,
                                nickname = p.NickName,
                                description = p.Description,
                                type_name = p.TypeName
                            }).ToArray()
                        } : null
                    })
                    .ToArray();

                var response = new
                {
                    status = "success",
                    components = components
                };

                return Task.FromResult(new CallToolResult()
                {
                    Content = [new TextContentBlock() 
                    { 
                        Text = JsonSerializer.Serialize(response, new JsonSerializerOptions 
                        { 
                            WriteIndented = true 
                        }), 
                    }]
                });
            }
            catch (Exception ex)
            {
                throw new McpProtocolException($"Error getting canvas components: {ex.Message}", ex);
            }
        }
    }
}