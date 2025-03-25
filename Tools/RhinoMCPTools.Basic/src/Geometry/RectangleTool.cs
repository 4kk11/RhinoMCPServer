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
    public class RectangleTool : IMCPTool
    {
        public string Name => "rectangle";
        public string Description => "Creates a rectangle from center point, width (x-direction), and height (y-direction).";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "center": {
                        "type": "object",
                        "description": "Center point of the rectangle",
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
                    "width": {
                        "type": "number",
                        "description": "Width of the rectangle (x-direction)",
                        "minimum": 0
                    },
                    "height": {
                        "type": "number",
                        "description": "Height of the rectangle (y-direction)",
                        "minimum": 0
                    }
                },
                "required": ["center", "width", "height"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("center", out var centerValue) ||
                !request.Arguments.TryGetValue("width", out var widthValue) ||
                !request.Arguments.TryGetValue("height", out var heightValue))
            {
                throw new McpServerException("Missing required arguments: 'center', 'width', and 'height' are required");
            }

            var centerElement = (JsonElement)centerValue;
            var width = Convert.ToDouble(widthValue.ToString());
            var height = Convert.ToDouble(heightValue.ToString());

            if (width <= 0 || height <= 0)
            {
                throw new McpServerException("Width and height must be greater than 0");
            }

            // 中心点の座標を取得
            var centerX = centerElement.GetProperty("x").GetDouble();
            var centerY = centerElement.GetProperty("y").GetDouble();
            var centerZ = centerElement.TryGetProperty("z", out var centerZElement) ? centerZElement.GetDouble() : 0.0;
            var center = new Point3d(centerX, centerY, centerZ);

            // 長方形の4頂点を計算
            var halfWidth = width / 2;
            var halfHeight = height / 2;

            var points = new Point3d[]
            {
                new Point3d(center.X - halfWidth, center.Y - halfHeight, center.Z), // 左下
                new Point3d(center.X + halfWidth, center.Y - halfHeight, center.Z), // 右下
                new Point3d(center.X + halfWidth, center.Y + halfHeight, center.Z), // 右上
                new Point3d(center.X - halfWidth, center.Y + halfHeight, center.Z), // 左上
                new Point3d(center.X - halfWidth, center.Y - halfHeight, center.Z)  // 左下（閉じるため）
            };

            var polyline = new Polyline(points);
            var rhinoDoc = RhinoDoc.ActiveDoc;
            var guid = rhinoDoc.Objects.AddPolyline(polyline);
            rhinoDoc.Views.Redraw();

            var response = new
            {
                status = "success",
                rectangle = new
                {
                    guid = guid.ToString(),
                    center = new { x = centerX, y = centerY, z = centerZ },
                    width = width,
                    height = height,
                    vertices = points.Take(4).Select(p => new { x = p.X, y = p.Y, z = p.Z }).ToArray()
                }
            };

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }), Type = "text" }]
            });
        }
    }
}