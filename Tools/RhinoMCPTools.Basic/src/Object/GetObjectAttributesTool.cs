using System;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Rhino;
using System.Text.Json;
using RhinoMCPServer.Common;
using System.Drawing;
using System.Collections.Specialized;
using System.Linq;

namespace RhinoMCPTools.Basic
{
    public class GetObjectAttributesTool : IMCPTool
    {
        public string Name => "get_object_attributes";
        public string Description => "Gets the attributes of a Rhino object.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "guid": {
                        "type": "string",
                        "description": "The GUID of the target Rhino object."
                    }
                },
                "required": ["guid"]
            }
            """);

        public Task<CallToolResult> ExecuteAsync(CallToolRequestParams request, McpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpProtocolException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("guid", out var guidValue))
            {
                throw new McpProtocolException("Missing required argument: 'guid' is required");
            }

            var rhinoDoc = RhinoDoc.ActiveDoc;
            if (!Guid.TryParse(guidValue.ToString(), out Guid objectGuid))
            {
                throw new McpProtocolException("Invalid GUID format");
            }

            var rhinoObject = rhinoDoc.Objects.Find(objectGuid);
            if (rhinoObject == null)
            {
                throw new McpProtocolException($"No object found with GUID: {objectGuid}");
            }

            var attributes = rhinoObject.Attributes;
            var response = new
            {
                status = "success",
                data = new
                {
                    guid = objectGuid.ToString(),
                    name = attributes.Name ?? string.Empty,
                    layer_index = attributes.LayerIndex,
                    layer_name = rhinoDoc.Layers[attributes.LayerIndex].Name,
                    object_color = new 
                    {
                        r = attributes.ObjectColor.R,
                        g = attributes.ObjectColor.G,
                        b = attributes.ObjectColor.B,
                        a = attributes.ObjectColor.A
                    },
                    color_source = attributes.ColorSource.ToString(),
                    plot_color = new
                    {
                        r = attributes.PlotColor.R,
                        g = attributes.PlotColor.G,
                        b = attributes.PlotColor.B,
                        a = attributes.PlotColor.A
                    },
                    plot_weight = attributes.PlotWeight,
                    visible = attributes.Visible,
                    mode = attributes.Mode.ToString(),
                    object_type = rhinoObject.ObjectType.ToString(),
                    user_text = attributes.GetUserStrings().AllKeys?.ToDictionary(
                        key => key ?? string.Empty,
                        key => attributes.GetUserStrings()[key] ?? string.Empty
                    ) ?? new Dictionary<string, string>()
                }
            };

            return Task.FromResult(new CallToolResult()
            {
                Content = [new TextContentBlock() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }) }]
            });
        }
    }
}