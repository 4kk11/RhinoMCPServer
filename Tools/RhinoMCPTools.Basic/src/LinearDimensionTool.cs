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
    public class LinearDimensionTool : IMCPTool
    {
        public string Name => "linear_dimension";
        public string Description => "Creates a linear dimension between two points in Rhino.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "start": {
                        "type": "object",
                        "description": "Start point of the dimension",
                        "properties": {
                            "x": {
                                "type": "number",
                                "description": "X coordinate of the start point"
                            },
                            "y": {
                                "type": "number",
                                "description": "Y coordinate of the start point"
                            },
                            "z": {
                                "type": "number",
                                "description": "Z coordinate of the start point",
                                "default": 0
                            }
                        },
                        "required": ["x", "y"]
                    },
                    "end": {
                        "type": "object",
                        "description": "End point of the dimension",
                        "properties": {
                            "x": {
                                "type": "number",
                                "description": "X coordinate of the end point"
                            },
                            "y": {
                                "type": "number",
                                "description": "Y coordinate of the end point"
                            },
                            "z": {
                                "type": "number",
                                "description": "Z coordinate of the end point",
                                "default": 0
                            }
                        },
                        "required": ["x", "y"]
                    },
                    "offset": {
                        "type": "number",
                        "description": "Offset distance for the dimension line from the measurement points",
                        "default": 1.0
                    }
                },
                "required": ["start", "end"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("start", out var startValue) ||
                !request.Arguments.TryGetValue("end", out var endValue))
            {
                throw new McpServerException("Missing required arguments: 'start' and 'end' points are required");
            }

            var startElement = (JsonElement)startValue;
            var endElement = (JsonElement)endValue;

            // 始点の座標を取得
            var startX = startElement.GetProperty("x").GetDouble();
            var startY = startElement.GetProperty("y").GetDouble();
            var startZ = startElement.TryGetProperty("z", out var startZElement) ? startZElement.GetDouble() : 0.0;
            var startPoint = new Point3d(startX, startY, startZ);

            // 終点の座標を取得
            var endX = endElement.GetProperty("x").GetDouble();
            var endY = endElement.GetProperty("y").GetDouble();
            var endZ = endElement.TryGetProperty("z", out var endZElement) ? endZElement.GetDouble() : 0.0;
            var endPoint = new Point3d(endX, endY, endZ);

            // オフセット値を取得（デフォルト値: 1.0）
            var offset = request.Arguments.TryGetValue("offset", out var offsetValue) ? 
                Convert.ToDouble(offsetValue.ToString()) : 1.0;

            var rhinoDoc = RhinoDoc.ActiveDoc;
            
            // 2点を通る線に平行なplaneを作成
            var dimensionLine = endPoint - startPoint;
            var dimensionNormal = Vector3d.CrossProduct(dimensionLine, Vector3d.ZAxis);
            dimensionNormal.Unitize();
            var plane = new Plane(startPoint, dimensionLine, dimensionNormal);

            // 3D点を2D点に変換
            double u, v;
            plane.ClosestParameter(startPoint, out u, out v);
            Point2d start2d = new Point2d(u, v);
            plane.ClosestParameter(endPoint, out u, out v);
            Point2d end2d = new Point2d(u, v);

            // オフセット位置を計算（2D）
            var dimLinePoint2d = new Point2d(0, offset);

            Console.WriteLine($"Start: {startPoint}, End: {endPoint}, Offset: {offset}");
            Console.WriteLine($"Plane: {plane}, Start2D: {start2d}, End2D: {end2d}, DimLine2D: {dimLinePoint2d}");

            // 寸法線オブジェクトを作成
            var dimension = new LinearDimension(
                plane,
                start2d,
                end2d,
                dimLinePoint2d);

            // 寸法線をドキュメントに追加
            var guid = rhinoDoc.Objects.AddLinearDimension(dimension);
            rhinoDoc.Views.Redraw();

            var response = new
            {
                status = "success",
                dimension = new
                {
                    guid = guid.ToString(),
                    start = new { x = startX, y = startY, z = startZ },
                    end = new { x = endX, y = endY, z = endZ },
                    offset = offset,
                    length = dimensionLine.Length,
                    scale = dimension.DimensionScale
                }
            };

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }), Type = "text" }]
            });
        }
    }
}