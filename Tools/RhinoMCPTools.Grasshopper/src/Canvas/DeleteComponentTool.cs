using System;
using System.Threading.Tasks;
using System.Text.Json;
using Grasshopper;
using Grasshopper.Kernel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RhinoMCPServer.Common;
using Rhino;

namespace RhinoMCPTools.Grasshopper
{
    public class DeleteComponentTool : IMCPTool
    {
        public string Name => "delete_component";
        public string Description => "Deletes a component from the Grasshopper canvas by its GUID.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "component_id": {
                        "type": "string",
                        "description": "The GUID of the component to delete"
                    }
                },
                "required": ["component_id"]
            }
            """);

        public Task<CallToolResult> ExecuteAsync(CallToolRequestParams request, McpServer? server)
        {
            try
            {
                // Validate request parameters
                if (request.Arguments is null)
                {
                    throw new McpProtocolException("Missing required arguments");
                }

                if (!request.Arguments.TryGetValue("component_id", out var componentValue))
                {
                    throw new McpProtocolException("Missing required parameter: component_id");
                }

                if (!Guid.TryParse(componentValue.ToString(), out var componentGuid))
                {
                    throw new McpProtocolException("Invalid GUID format for component_id");
                }

                // Get active Grasshopper document
                GH_Document? doc = Instances.ActiveDocument;
                if (doc == null)
                {
                    throw new McpProtocolException("No active Grasshopper document found");
                }

                // Find the component to delete
                var component = doc.FindObject(componentGuid, false);
                if (component == null)
                {
                    throw new McpProtocolException($"Component with GUID {componentGuid} not found");
                }

                // Store component info for response
                var componentInfo = new
                {
                    guid = component.InstanceGuid.ToString(),
                    name = component.Name,
                    position = new
                    {
                        x = component.Attributes.Pivot.X,
                        y = component.Attributes.Pivot.Y
                    }
                };

                // Delete the component
                doc.RemoveObject(component, false);

                // Force solution update and redraw
                doc.NewSolution(false);
                RhinoApp.InvokeOnUiThread(() =>
                {
                    Instances.RedrawCanvas();
                });

                var response = new
                {
                    status = "success",
                    deleted_component = componentInfo
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
                throw new McpProtocolException($"Error deleting component: {ex.Message}", ex);
            }
        }
    }
}