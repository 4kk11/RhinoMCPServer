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
                    "name": {
                        "type": "string",
                        "description": "The name of the layer to create."
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
                    },
                    "parent_name": {
                        "type": "string",
                        "description": "The name of the parent layer (optional)."
                    }
                },
                "required": ["name"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("name", out var nameValue))
            {
                throw new McpServerException("Missing required argument 'name'");
            }

            var layerName = nameValue.ToString();
            var rhinoDoc = RhinoDoc.ActiveDoc;

            // 同名のレイヤーが既に存在するかチェック
            var existingLayer = rhinoDoc.Layers.FindByFullPath(layerName, RhinoMath.UnsetIntIndex);
            if (existingLayer != RhinoMath.UnsetIntIndex)
            {
                throw new McpServerException($"Layer '{layerName}' already exists");
            }

            // 新しいレイヤーの作成
            var layer = new Layer
            {
                Name = layerName,
                Color = Color.Black // デフォルトカラーを黒に設定
            };

            // オプションのプロパティを設定
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
                        layer.Color = Color.FromArgb(r, g, b);
                    }
                    catch
                    {
                        throw new McpServerException("Invalid color format. Use hex format (e.g., '#FF0000')");
                    }
                }
            }

            if (request.Arguments.TryGetValue("visible", out var visibleValue))
            {
                layer.IsVisible = Convert.ToBoolean(visibleValue.ToString());
            }

            if (request.Arguments.TryGetValue("locked", out var lockedValue))
            {
                layer.IsLocked = Convert.ToBoolean(lockedValue.ToString());
            }

            // 親レイヤーの設定（指定されている場合）
            if (request.Arguments.TryGetValue("parent_name", out var parentNameValue))
            {
                var parentName = parentNameValue.ToString();
                var parentIndex = rhinoDoc.Layers.FindByFullPath(parentName, RhinoMath.UnsetIntIndex);
                if (parentIndex == RhinoMath.UnsetIntIndex)
                {
                    throw new McpServerException($"Parent layer '{parentName}' not found");
                }
                layer.ParentLayerId = rhinoDoc.Layers[parentIndex].Id;
            }

            // レイヤーの追加
            var index = rhinoDoc.Layers.Add(layer);
            if (index == -1)
            {
                throw new McpServerException("Failed to create layer");
            }

            rhinoDoc.Views.Redraw();

            var response = new
            {
                status = "success",
                layer = new
                {
                    name = layer.Name,
                    index = index,
                    id = layer.Id,
                    color = $"#{layer.Color.R:X2}{layer.Color.G:X2}{layer.Color.B:X2}",
                    visible = layer.IsVisible,
                    locked = layer.IsLocked,
                    parent_id = layer.ParentLayerId
                }
            };

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }), Type = "text" }]
            });
        }
    }
}