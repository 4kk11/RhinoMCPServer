using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using Rhino.Geometry;
using System.Text.Json;
using RhinoMCPServer.Common;
using Rhino.DocObjects;

namespace RhinoMCPTools.Basic
{
    public class CreateGuidTextDotTool : IMCPTool
    {
        public string Name => "create_guid_text_dots";
        public string Description => "Creates text dots with GUID for objects visible in the current view.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "offset": {
                        "type": "number",
                        "description": "Offset distance for the text dot from the object's center.",
                        "default": 1.0
                    }
                }
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            var rhinoDoc = RhinoDoc.ActiveDoc;
            var activeView = rhinoDoc.Views.ActiveView;
            var viewport = activeView.ActiveViewport;

            // オフセット値を取得（デフォルト値: 1.0）
            var offset = 1.0;
            if (request.Arguments != null && request.Arguments.TryGetValue("offset", out var offsetValue))
            {
                offset = Convert.ToDouble(offsetValue.ToString());
            }

            // ドキュメント内のすべてのオブジェクトを取得
            var command = "SelVisible " + " _Enter";
            RhinoApp.RunScript(command, true);
            var selectedObjects = rhinoDoc.Objects.GetSelectedObjects(false, false);
            var selectedObjectIds = selectedObjects.Select(obj => obj.Id).ToList();
            rhinoDoc.Objects.UnselectAll();

            var createdDots = new List<(string guid, Point3d location)>();

            foreach (var id in selectedObjectIds)
            {
                var obj = rhinoDoc.Objects.Find(id);
                // オブジェクトのバウンディングボックスの中心を取得
                var bbox = obj.Geometry.GetBoundingBox(true);
                var center = bbox.Center;
                
                // オフセットを適用（Z方向に）
                var dotLocation = new Point3d(center.X, center.Y, center.Z + offset);
                
                // テキストドットを作成
                var textDot = new TextDot(obj.Id.ToString(), dotLocation);
                textDot.FontHeight = 12;
                var dotGuid = rhinoDoc.Objects.AddTextDot(textDot);

                createdDots.Add((dotGuid.ToString(), dotLocation));
            }

            rhinoDoc.Views.Redraw();

            var response = new
            {
                status = "success",
                dots = createdDots.Select(dot => new
                {
                    guid = dot.guid,
                    location = new
                    {
                        x = dot.location.X,
                        y = dot.location.Y,
                        z = dot.location.Z
                    }
                }).ToArray()
            };

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }), Type = "text" }]
            });
        }
    }
}