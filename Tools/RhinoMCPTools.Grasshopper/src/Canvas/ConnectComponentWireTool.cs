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
using Rhino;

namespace RhinoMCPTools.Grasshopper
{
    public class ConnectComponentWireTool : IMCPTool
    {
        public string Name => "connect_component_wire";
        public string Description => "Connects two component parameters with a wire in the Grasshopper canvas.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "source_param_id": {
                        "type": "string",
                        "description": "The GUID of the source parameter to connect from"
                    },
                    "target_param_id": {
                        "type": "string",
                        "description": "The GUID of the target parameter to connect to"
                    }
                },
                "required": ["source_param_id", "target_param_id"]
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

                if (!request.Arguments.TryGetValue("source_param_id", out var sourceValue) ||
                    !request.Arguments.TryGetValue("target_param_id", out var targetValue))
                {
                    throw new McpProtocolException("Missing required parameters: source_param_id and/or target_param_id");
                }

                if (!Guid.TryParse(sourceValue.ToString(), out var sourceGuid) ||
                    !Guid.TryParse(targetValue.ToString(), out var targetGuid))
                {
                    throw new McpProtocolException("Invalid GUID format for parameters");
                }

                // Get active Grasshopper document
                GH_Document? doc = Instances.ActiveDocument;
                if (doc == null)
                {
                    throw new McpProtocolException("No active Grasshopper document found");
                }

                // Find source and target parameters
                var sourceParam = doc.FindParameter(sourceGuid);
                var targetParam = doc.FindParameter(targetGuid);

                if (sourceParam == null || targetParam == null)
                {
                    throw new McpProtocolException("One or both parameters not found in the document");
                }

                // Validate parameter types (source must be output, target must be input)
                if (!(sourceParam is IGH_Param sourceGHParam) || !(targetParam is IGH_Param targetGHParam))
                {
                    throw new McpProtocolException("Invalid parameter types");
                }

                var sourceComponent = sourceParam.Attributes.GetTopLevel.DocObject;
                var targetComponent = targetParam.Attributes.GetTopLevel.DocObject;

                if (sourceComponent == null || targetComponent == null)
                {
                    throw new McpProtocolException("Source or target component not found");
                }

                if (sourceComponent is IGH_Component && sourceParam.Kind != GH_ParamKind.output)
                {
                    throw new McpProtocolException("Source parameter is not an output parameter");
                }

                if (targetComponent is IGH_Component && targetParam.Kind != GH_ParamKind.input)
                {
                    throw new McpProtocolException("Target parameter is not an input parameter");
                }

                // Connect the parameters
                targetGHParam.AddSource(sourceGHParam);

                // Force solution update and redraw
                doc.NewSolution(false);
                RhinoApp.InvokeOnUiThread(() =>
                {
                    Instances.RedrawCanvas();
                });

                var response = new
                {
                    status = "success",
                    connection = new
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
                throw new McpProtocolException($"Error connecting component wire: {ex.Message}", ex);
            }
        }
    }
}