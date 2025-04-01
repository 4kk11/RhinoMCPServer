using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Grasshopper;
using Grasshopper.Kernel;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Grasshopper.Canvas
{
    /// <summary>
    /// Tool to retrieve detailed information about a specific component, including its input/output parameters and connections
    /// </summary>
    public class GetComponentInfoTool : IMCPTool
    {
        public string Name => "get_component_info";
        public string Description => "Retrieves detailed information about a specific Grasshopper component, including its parameters and connection states";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "component_id": {
                        "type": "string",
                        "description": "The GUID of the component to get information for"
                    }
                },
                "required": ["component_id"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            try
            {
                // Validate parameters
                if (request.Arguments is null)
                {
                    throw new McpServerException("Missing required arguments");
                }

                if (!request.Arguments.TryGetValue("component_id", out var componentValue))
                {
                    throw new McpServerException("component_id parameter is required");
                }

                if (!Guid.TryParse(componentValue.ToString(), out var componentGuid))
                {
                    throw new McpServerException("Invalid GUID format");
                }

                // Get active Grasshopper document
                GH_Document? doc = Instances.ActiveDocument;
                if (doc == null)
                {
                    throw new McpServerException("No active Grasshopper document found");
                }

                // Find the component
                var obj = doc.FindObject(componentGuid, false);
                if (obj == null)
                {
                    throw new McpServerException($"Component with GUID {componentGuid} not found");
                }

                // Collect basic component information
                var componentInfo = new
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
                    }
                };

                // Collect parameter information (only for components)
                var parameterInfo = obj is IGH_Component component ? new
                {
                    input = component.Params.Input.Select(p => new
                    {
                        name = p.Name,
                        nickname = p.NickName,
                        description = p.Description,
                        type_name = p.TypeName,
                        param_id = p.InstanceGuid.ToString(),
                        optional = p.Optional,
                        // Connection information
                        connections = p.Sources.Select(source => new
                        {
                            source_param_id = source.InstanceGuid.ToString(),
                            source_component_id = source.Attributes.GetTopLevel.DocObject.InstanceGuid.ToString(),
                            source_name = source.Name,
                            source_type = source.TypeName
                        }).ToArray()
                    }).ToArray(),
                    output = component.Params.Output.Select(p => new
                    {
                        name = p.Name,
                        nickname = p.NickName,
                        description = p.Description,
                        type_name = p.TypeName,
                        param_id = p.InstanceGuid.ToString(),
                        // Connection information
                        connections = p.Recipients.Select(recipient => new
                        {
                            target_param_id = recipient.InstanceGuid.ToString(),
                            target_component_id = recipient.Attributes.GetTopLevel.DocObject.InstanceGuid.ToString(),
                            target_name = recipient.Name,
                            target_type = recipient.TypeName
                        }).ToArray()
                    }).ToArray()
                } : null;

                // Build response
                var response = new
                {
                    status = "success",
                    component = new
                    {
                        info = componentInfo,
                        parameters = parameterInfo
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
                throw new McpServerException($"Error getting component information: {ex.Message}", ex);
            }
        }
    }
}