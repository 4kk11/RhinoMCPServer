using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using RhinoMCPServer.Common;
using SixLabors.ImageSharp;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System.Linq;
using Rhino.Geometry;
using Rhino.DocObjects;
using System;
using System.Collections.Generic;

namespace RhinoMCPTools.Basic
{
    public class CaptureViewportTool : IMCPTool
    {
        private static readonly string[] Symbols = "ABCDEFGHIJKLMNOPQRSTUVWXYZ".Select(c => c.ToString()).ToArray();

        public string Name => "capture_viewport";
        public string Description => """
            Captures the specified Rhino viewport as an image for various purposes:
            • Documentation: Records the current state of 3D workspace
            • Communication: Facilitates design reviews and progress sharing
            • Debugging: Helps verify object positions and relationships
            
            Features:
            • Simple symbol labeling (A, B, C...) for objects
            • Automatic mapping between symbols and object IDs
            • Clear and intuitive text annotations
            """;

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "viewportName": {
                        "type": "string",
                        "description": "The name of the viewport to capture. If not specified, the active viewport will be used."
                    },
                    "width": {
                        "type": "number",
                        "description": "The width of the captured image in pixels. If not specified, the current viewport width will be used."
                    },
                    "height": {
                        "type": "number",
                        "description": "The height of the captured image in pixels. If not specified, the current viewport height will be used."
                    },
                    "format": {
                        "type": "string",
                        "enum": ["png", "jpg"],
                        "description": "The image format to use for the capture.",
                        "default": "png"
                    },
                    "show_object_labels": {
                        "type": "boolean",
                        "description": "Whether to display simple symbol labels (A, B, C...) for objects in the viewport.",
                        "default": false
                    },
                    "font_height": {
                        "type": "number",
                        "description": "Font size for the label text.",
                        "default": 20.0
                    }
                }
            }
            """);

        public async Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            try
            {
                var rhinoDoc = RhinoDoc.ActiveDoc;
                var activeView = rhinoDoc.Views.ActiveView;

                // パラメータの取得
                string? viewportName = null;
                int? width = null;
                int? height = null;
                string format = "png";
                bool showObjectLabels = false;
                double fontHeight = 20.0;

                if (request.Arguments != null)
                {
                    if (request.Arguments.TryGetValue("viewportName", out var viewportValue))
                    {
                        viewportName = viewportValue.ToString();
                    }
                    if (request.Arguments.TryGetValue("width", out var widthValue) && widthValue is JsonElement widthElement)
                    {
                        width = widthElement.GetInt32();
                    }
                    if (request.Arguments.TryGetValue("height", out var heightValue) && heightValue is JsonElement heightElement)
                    {
                        height = heightElement.GetInt32();
                    }
                    if (request.Arguments.TryGetValue("format", out var formatValue))
                    {
                        format = formatValue.ToString()?.ToLower() ?? "png";
                    }
                    if (request.Arguments.TryGetValue("show_object_labels", out var showLabelsValue) && showLabelsValue is JsonElement showLabelsElement)
                    {
                        showObjectLabels = showLabelsElement.GetBoolean();
                    }
                    if (request.Arguments.TryGetValue("font_height", out var fontHeightValue) && fontHeightValue is JsonElement fontHeightElement)
                    {
                        fontHeight = fontHeightElement.GetDouble();
                    }
                }

                // ビューポートの取得
                var view = string.IsNullOrEmpty(viewportName)
                    ? activeView
                    : rhinoDoc.Views.Find(viewportName, false);

                if (view == null)
                {
                    throw new McpServerException($"Viewport not found: {viewportName ?? "active"}");
                }

                // オブジェクトマッピングの初期化
                var objectMapping = new Dictionary<string, string>(); // Symbol -> GUID
                using var textDotManager = showObjectLabels ? new TextDotManager(rhinoDoc) : null;

                if (showObjectLabels)
                {
                    RhinoApp.InvokeOnUiThread(() =>
                    {
                        // 表示されているオブジェクトを取得
                        var selectedObjectIds = new List<Guid>();
                        var command = "SelVisible " + " _Enter";
                        RhinoApp.RunScript(command, true);
                        var selectedObjects = rhinoDoc.Objects.GetSelectedObjects(false, false).ToList();
                        selectedObjectIds.AddRange(selectedObjects.Select(obj => obj.Id));
                        rhinoDoc.Objects.UnselectAll();

                        // オブジェクトラベルの設定
                        var attributes = new ObjectAttributes();
                        attributes.ObjectColor = System.Drawing.Color.White;

                        for (int i = 0; i < selectedObjectIds.Count && i < Symbols.Length; i++)
                        {
                            var obj = rhinoDoc.Objects.Find(selectedObjectIds[i]);
                            if (obj != null)
                            {
                                var symbol = Symbols[i];
                                var bbox = obj.Geometry.GetBoundingBox(true);
                                var dotLocation = bbox.Center;
                                
                                textDotManager?.AddTextDot(dotLocation, symbol, (int)fontHeight, attributes);
                                objectMapping[symbol] = selectedObjectIds[i].ToString();
                            }
                        }
                    });
                }

                // キャプチャサイズの設定
                var size = new System.Drawing.Size(
                    width ?? view.ClientRectangle.Width,
                    height ?? view.ClientRectangle.Height
                );

                // ビューポートのキャプチャ
                using var bitmap = view.CaptureToBitmap(size);
                if (bitmap == null)
                {
                    throw new McpServerException("Failed to capture viewport");
                }

                // bitmapをMemoryStreamに保存
                using var intermediateStream = new MemoryStream();
                bitmap.Save(intermediateStream, ImageFormat.Png);
                intermediateStream.Seek(0, SeekOrigin.Begin);

                // ImageSharpを使用して画像を処理
                using var image = await SixLabors.ImageSharp.Image.LoadAsync(intermediateStream);
                using var outputStream = new MemoryStream();

                // 画像のエンコード
                if (format == "jpg")
                {
                    await image.SaveAsJpegAsync(outputStream);
                }
                else
                {
                    await image.SaveAsPngAsync(outputStream);
                }

                var base64Image = Convert.ToBase64String(outputStream.ToArray());

                // レスポンスの作成
                var response = new
                {
                    status = "success",
                    objects = objectMapping.Select(kvp => new { symbol = kvp.Key, guid = kvp.Value }).ToArray(),
                    image_info = new
                    {
                        format,
                        width = size.Width,
                        height = size.Height
                    }
                };

                return new CallToolResponse()
                {
                    Content = 
                    [
                        new Content()
                        {
                            Type = "text",
                            Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true })
                        },
                        new Content()
                        {
                            Type = "image",
                            Data = base64Image,
                            MimeType = format == "jpg" ? "image/jpeg" : "image/png"
                        }
                    ]
                };
            }
            catch (Exception ex)
            {
                throw new McpServerException($"Error capturing viewport: {ex.Message}", ex);
            }
        }
    }
}