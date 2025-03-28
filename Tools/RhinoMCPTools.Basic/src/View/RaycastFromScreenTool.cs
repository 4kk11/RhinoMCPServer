using System;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using Rhino.Geometry;
using Rhino.DocObjects;
using System.Text.Json;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Basic
{
    public class RaycastFromScreenTool : IMCPTool
    {
        public string Name => "raycast_from_screen";
        public string Description => """
            Performs a raycast from screen coordinates and returns information about the hit object.
            
            Features:
            • Converts screen coordinates to a 3D ray
            • Detects the first object hit by the ray
            • Returns detailed information about the hit object including:
              - Object GUID
              - Hit point coordinates
              - Surface parameters at hit point
              - Distance from ray origin
            """;

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "x": {
                        "type": "number",
                        "description": "Normalized x-coordinate (0.0 to 1.0)",
                        "minimum": 0.0,
                        "maximum": 1.0
                    },
                    "y": {
                        "type": "number",
                        "description": "Normalized y-coordinate (0.0 to 1.0)",
                        "minimum": 0.0,
                        "maximum": 1.0
                    },
                    "viewportName": {
                        "type": "string",
                        "description": "The name of the viewport to use. If not specified, the active viewport will be used."
                    }
                },
                "required": ["x", "y"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("x", out var xValue) || !request.Arguments.TryGetValue("y", out var yValue))
            {
                throw new McpServerException("Missing required arguments: 'x' and 'y' are required");
            }

            // 0.0-1.0の正規化された値を取得
            var normalizedX = Convert.ToDouble(xValue.ToString());
            var normalizedY = Convert.ToDouble(yValue.ToString());

            // 値の範囲チェック
            if (normalizedX < 0.0 || normalizedX > 1.0 || normalizedY < 0.0 || normalizedY > 1.0)
            {
                throw new McpServerException("Coordinates must be between 0.0 and 1.0");
            }

            var rhinoDoc = RhinoDoc.ActiveDoc;
            var view = string.IsNullOrEmpty(request.Arguments.TryGetValue("viewportName", out var viewportValue) ? viewportValue.ToString() : null)
                ? rhinoDoc.Views.ActiveView
                : rhinoDoc.Views.Find(viewportValue.ToString() ?? "", false);

            if (view == null)
            {
                throw new McpServerException("Viewport not found");
            }

            // スクリーン座標からレイを生成
            var viewport = view.ActiveViewport;
            
            // ビューポートのサイズを取得して正規化された座標をピクセル座標に変換
            var viewportWidth = viewport.Size.Width;
            var viewportHeight = viewport.Size.Height;
            var pixelX = normalizedX * viewportWidth;
            var pixelY = normalizedY * viewportHeight;
            
            var startPt = new Point2d(pixelX, pixelY);
            Line line = viewport.ClientToWorld(startPt);
            var cameraPos = viewport.CameraLocation;
            var rayVec = line.To - cameraPos;
            var ray = new Ray3d(cameraPos, rayVec);

            // ジオメトリを収集
            var geometries = rhinoDoc.Objects.Select(obj => obj.Geometry);

            // レイキャストを実行
            var raycastResults = Rhino.Geometry.Intersect.Intersection.RayShoot(geometries, ray, 1);
            if (raycastResults == null || raycastResults.Length == 0)
            {
                // ヒットしなかった場合
                return Task.FromResult(new CallToolResponse()
                {
                    Content = [new Content()
                    {
                        Text = JsonSerializer.Serialize(
                            new { status = "success", hit = false },
                            new JsonSerializerOptions { WriteIndented = true }
                        ),
                        Type = "text"
                    }]
                });
            }

            // 最初のヒット結果を使用
            var hitResult = raycastResults[0];
            var hitObject = rhinoDoc.Objects.ElementAt(hitResult.GeometryIndex);
            var hitGeometry = hitObject.Geometry;

            RhinoApp.InvokeOnUiThread(() =>
            {
                rhinoDoc.Objects.Select(hitObject.Id);
                rhinoDoc.Views.Redraw();
            });
            

            var response = new
            {
                status = "success",
                hit = true,
                object_info = new
                {
                    guid = hitObject.Id.ToString(),
                    type = hitGeometry.GetType().Name,
                    hit_point = new { x = hitResult.Point.X, y = hitResult.Point.Y, z = hitResult.Point.Z },
                    geometry_index = hitResult.GeometryIndex,
                    layer = hitObject.Attributes.LayerIndex
                }
            };

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() 
                { 
                    Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }), 
                    Type = "text" 
                }]
            });
        }
    }
}