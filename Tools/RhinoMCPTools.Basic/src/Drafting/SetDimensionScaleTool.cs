using System;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System.Text.Json;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Basic
{
    public class SetDimensionScaleTool : IMCPTool
    {
        public string Name => "set_dimension_scale";
        public string Description => "Sets the dimension scale of a dimension object in Rhino.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "guid": {
                        "type": "string",
                        "description": "The GUID of the target dimension object."
                    },
                    "scale": {
                        "type": "number",
                        "description": "The new dimension scale value.",
                        "minimum": 0
                    }
                },
                "required": ["guid", "scale"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("guid", out var guidValue) ||
                !request.Arguments.TryGetValue("scale", out var scaleValue))
            {
                throw new McpServerException("Missing required arguments: 'guid' and 'scale' are required");
            }

            var rhinoDoc = RhinoDoc.ActiveDoc;
            if (!Guid.TryParse(guidValue.ToString(), out Guid objectGuid))
            {
                throw new McpServerException("Invalid GUID format");
            }

            var rhinoObject = rhinoDoc.Objects.Find(objectGuid);
            if (rhinoObject == null)
            {
                throw new McpServerException($"No object found with GUID: {objectGuid}");
            }

            var dimension = rhinoObject.Geometry as Dimension;
            if (dimension == null)
            {
                throw new McpServerException("The specified object is not a dimension object");
            }

            var newScale = Convert.ToDouble(scaleValue.ToString());
            if (newScale <= 0)
            {
                throw new McpServerException("Dimension scale must be greater than 0");
            }

            // メインスレッドで寸法のスケールを設定
            RhinoApp.InvokeOnUiThread(() =>
            {
                dimension.DimensionScale = newScale;
                rhinoObject.CommitChanges();
                rhinoDoc.Views.Redraw();
            });

            var response = new
            {
                status = "success",
                dimension = new
                {
                    guid = objectGuid.ToString(),
                    new_dimension_scale = newScale
                }
            };

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }), Type = "text" }]
            });
        }
    }
}