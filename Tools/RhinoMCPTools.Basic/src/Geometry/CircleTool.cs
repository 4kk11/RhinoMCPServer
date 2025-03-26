using System;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using Rhino.Geometry;
using System.Text.Json;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Basic
{
    public class CircleTool : IMCPTool
    {
        public string Name => "circle";
        public string Description => "Creates a circle from center point and radius.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "center": {
                        "type": "object",
                        "description": "Center point of the circle",
                        "properties": {
                            "x": {
                                "type": "number",
                                "description": "X coordinate of the center point"
                            },
                            "y": {
                                "type": "number",
                                "description": "Y coordinate of the center point"
                            },
                            "z": {
                                "type": "number",
                                "description": "Z coordinate of the center point",
                                "default": 0
                            }
                        },
                        "required": ["x", "y"]
                    },
                    "radius": {
                        "type": "number",
                        "description": "Radius of the circle",
                        "minimum": 0
                    }
                },
                "required": ["center", "radius"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("center", out var centerValue) ||
                !request.Arguments.TryGetValue("radius", out var radiusValue))
            {
                throw new McpServerException("Missing required arguments: 'center' and 'radius' are required");
            }

            var centerElement = (JsonElement)centerValue;
            var radius = Convert.ToDouble(radiusValue.ToString());

            if (radius <= 0)
            {
                throw new McpServerException("Radius must be greater than 0");
            }

            // 中心点の座標を取得
            var centerX = centerElement.GetProperty("x").GetDouble();
            var centerY = centerElement.GetProperty("y").GetDouble();
            var centerZ = centerElement.TryGetProperty("z", out var centerZElement) ? centerZElement.GetDouble() : 0.0;
            var center = new Point3d(centerX, centerY, centerZ);

            // XY平面上に円を作成
            var circle = new Circle(new Plane(center, Vector3d.ZAxis), radius);
            var rhinoDoc = RhinoDoc.ActiveDoc;
            var guid = rhinoDoc.Objects.AddCircle(circle);
            rhinoDoc.Views.Redraw();

            var response = new
            {
                status = "success",
                circle = new
                {
                    guid = guid.ToString(),
                    center = new { x = centerX, y = centerY, z = centerZ },
                    radius = radius
                }
            };

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }), Type = "text" }]
            });
        }
    }
}