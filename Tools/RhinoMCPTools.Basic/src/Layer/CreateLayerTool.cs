using System;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using Rhino.DocObjects;
using System.Text.Json;
using RhinoMCPServer.Common;
using System.Drawing;

namespace RhinoMCPTools.Basic
{
    public class CreateLayerTool : IMCPTool
    {
        public string Name => "create_layer";
        public string Description => "Creates a new layer in Rhino.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "full_path": {
                        "type": "string",
                        "description": "The full path of the layer to create (e.g. 'Parent::Child::Grandchild')."
                    },
                    "color": {
                        "type": "string",
                        "description": "The color of the layer in hex format (e.g., '#FF0000' for red).",
                        "pattern": "^#[0-9A-Fa-f]{6}$",
                        "default": "#000000"
                    },
                    "visible": {
                        "type": "boolean",
                        "description": "Whether the layer is visible.",
                        "default": true
                    },
                    "locked": {
                        "type": "boolean",
                        "description": "Whether the layer is locked.",
                        "default": false
                    }
                },
                "required": ["full_path"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("full_path", out var pathValue))
            {
                throw new McpServerException("Missing required argument 'full_path'");
            }

            var fullPath = pathValue.ToString();
            var rhinoDoc = RhinoDoc.ActiveDoc;

            // 同名のレイヤーが既に存在するかチェック
            var existingLayer = rhinoDoc.Layers.FindByFullPath(fullPath, RhinoMath.UnsetIntIndex);
            if (existingLayer != RhinoMath.UnsetIntIndex)
            {
                throw new McpServerException($"Layer '{fullPath}' already exists");
            }

            // カラーの取得
            var color = Color.Black; // デフォルトカラー
            if (request.Arguments.TryGetValue("color", out var colorValue))
            {
                var colorStr = colorValue.ToString();
                if (colorStr.StartsWith("#") && colorStr.Length == 7)
                {
                    try
                    {
                        var r = Convert.ToByte(colorStr.Substring(1, 2), 16);
                        var g = Convert.ToByte(colorStr.Substring(3, 2), 16);
                        var b = Convert.ToByte(colorStr.Substring(5, 2), 16);
                        color = Color.FromArgb(r, g, b);
                    }
                    catch
                    {
                        throw new McpServerException("Invalid color format. Use hex format (e.g., '#FF0000')");
                    }
                }
            }

            // レイヤーの追加
            var index = rhinoDoc.Layers.AddPath(fullPath, color);
            if (index == RhinoMath.UnsetIntIndex)
            {
                throw new McpServerException("Failed to create layer");
            }

            // 作成されたレイヤーの取得と設定の更新
            var layer = rhinoDoc.Layers[index];

            // 表示/非表示の設定
            if (request.Arguments.TryGetValue("visible", out var visibleValue))
            {
                layer.IsVisible = Convert.ToBoolean(visibleValue.ToString());
            }

            // ロック状態の設定
            if (request.Arguments.TryGetValue("locked", out var lockedValue))
            {
                layer.IsLocked = Convert.ToBoolean(lockedValue.ToString());
            }

            // プロパティの変更を適用
            RhinoApp.InvokeOnUiThread(() =>
            {
                rhinoDoc.Layers.Modify(layer, index, quiet: true);
                rhinoDoc.Views.Redraw();
            });

            var response = new
            {
                status = "success",
                layer = new
                {
                    full_path = fullPath,
                    index = index,
                    id = layer.Id,
                    color = $"#{layer.Color.R:X2}{layer.Color.G:X2}{layer.Color.B:X2}",
                    visible = layer.IsVisible,
                    locked = layer.IsLocked
                }
            };

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }), Type = "text" }]
            });
        }
    }
}