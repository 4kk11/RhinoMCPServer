using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Grasshopper;
using Grasshopper.Kernel;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using RhinoMCPServer.Common;
using Rhino;

namespace RhinoMCPTools.Grasshopper
{
    public class DisconnectComponentWireTool : IMCPTool
    {
        public string Name => "disconnect_component_wire";
        public string Description => "Disconnects a wire between two component parameters in the Grasshopper canvas.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "source_param_id": {
                        "type": "string",
                        "description": "The GUID of the source parameter to disconnect from"
                    },
                    "target_param_id": {
                        "type": "string",
                        "description": "The GUID of the target parameter to disconnect from"
                    }
                },
                "required": ["source_param_id", "target_param_id"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            try
            {
                // Validate request parameters
                if (request.Arguments is null)
                {
                    throw new McpServerException("Missing required arguments");
                }

                if (!request.Arguments.TryGetValue("source_param_id", out var sourceValue) ||
                    !request.Arguments.TryGetValue("target_param_id", out var targetValue))
                {
                    throw new McpServerException("Missing required parameters: source_param_id and/or target_param_id");
                }

                if (!Guid.TryParse(sourceValue.ToString(), out var sourceGuid) ||
                    !Guid.TryParse(targetValue.ToString(), out var targetGuid))
                {
                    throw new McpServerException("Invalid GUID format for parameters");
                }

                // Get active Grasshopper document
                GH_Document? doc = Instances.ActiveDocument;
                if (doc == null)
                {
                    throw new McpServerException("No active Grasshopper document found");
                }

                // Find source and target parameters
                var sourceParam = doc.FindParameter(sourceGuid);
                var targetParam = doc.FindParameter(targetGuid);

                if (sourceParam == null || targetParam == null)
                {
                    throw new McpServerException("One or both parameters not found in the document");
                }

                // Validate parameter types
                if (!(sourceParam is IGH_Param sourceGHParam) || !(targetParam is IGH_Param targetGHParam))
                {
                    throw new McpServerException("Invalid parameter types");
                }

                // Check if the parameters are actually connected
                if (!targetGHParam.Sources.Contains(sourceGHParam))
                {
                    throw new McpServerException("The specified parameters are not connected");
                }

                // Disconnect the parameters
                targetGHParam.RemoveSource(sourceGHParam);

                // Force solution update and redraw
                doc.NewSolution(false);
                RhinoApp.InvokeOnUiThread(() =>
                {
                    Instances.RedrawCanvas();
                });

                var response = new
                {
                    status = "success",
                    disconnection = new
                    {
                        source = new
                        {
                            guid = sourceGuid.ToString(),
                            name = sourceGHParam.Name,
                        },
                        target = new
                        {
                            guid = targetGuid.ToString(),
                            name = targetGHParam.Name,
                        }
                    }
                };

                return Task.FromResult(new CallToolResponse()
                {
                    Content = [new Content() 
                    { 
                        Text = JsonSerializer.Serialize(response, new JsonSerializerOptions 
                        { 
                            WriteIndented = true 
                        }), 
                        Type = "text" 
                    }]
                });
            }
            catch (Exception ex)
            {
                throw new McpServerException($"Error disconnecting component wire: {ex.Message}", ex);
            }
        }
    }
}