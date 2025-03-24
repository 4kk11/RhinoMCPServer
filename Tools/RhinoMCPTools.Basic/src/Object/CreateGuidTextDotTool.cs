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
using System.Xml.Schema;

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
                    "font_height": {
                        "type": "number",
                        "description": "Font height for the text dot.",
                        "default": 12.0
                    }
                }
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            var rhinoDoc = RhinoDoc.ActiveDoc;
            var activeView = rhinoDoc.Views.ActiveView;
            var viewport = activeView.ActiveViewport;

            // パラメータの取得（デフォルト値を設定）
            var fontHeight = 12.0;

            if (request.Arguments != null)
            {
                if (request.Arguments.TryGetValue("font_height", out var fontHeightValue))
                {
                    fontHeight = Convert.ToDouble(fontHeightValue.ToString());
                }
            }
            
            List<Guid> selectedObjectIds = new List<Guid>();
            
            // メインスレッドでSelVisibleコマンドを実行
            RhinoApp.InvokeOnUiThread(() =>
            {
                var command = "SelVisible " + " _Enter";
                RhinoApp.RunScript(command, true);
                var selectedObjects = rhinoDoc.Objects.GetSelectedObjects(false, false);
                selectedObjectIds.AddRange(selectedObjects.Select(obj => obj.Id));
                rhinoDoc.Objects.UnselectAll();
            });

            var createdDots = new List<(string guid, Point3d location)>();
            foreach (var id in selectedObjectIds)
            {
                var obj = rhinoDoc.Objects.Find(id);
                // オブジェクトのバウンディングボックスの中心を取得
                var bbox = obj.Geometry.GetBoundingBox(true);
                var center = bbox.Center;
                var dotLocation = new Point3d(center.X, center.Y, center.Z);
                
                // テキストドットを作成
                var textDot = new TextDot(obj.Id.ToString(), dotLocation);
                textDot.FontHeight = (int)fontHeight;
                var dotGuid = rhinoDoc.Objects.AddTextDot(textDot);
                var guid = dotGuid.ToString();

                createdDots.Add((guid, dotLocation));
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