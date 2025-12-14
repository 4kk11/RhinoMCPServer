using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Rhino;
using Rhino.DocObjects;
using RhinoMCPServer.Common;
using RhinoMCPTools.Basic.Helpers;
using SixLabors.ImageSharp;

namespace RhinoMCPTools.Basic
{
    /// <summary>
    /// BIMスタイルのセマンティックセグメンテーション画像をエクスポートするツール
    ///
    /// 責務:
    /// - element_type属性に基づくオブジェクトの色分け
    /// - 通常レンダリングとカラーマップの両方を出力
    /// - 色と属性の対応表をJSON形式で出力
    /// </summary>
    public class ExportSemanticViewTool : IMCPTool
    {
        public string Name => "export_semantic_view";

        public string Description => """
            Exports viewport images with semantic segmentation masks based on element_type attributes.

            Use cases:
            - AI/ML training data generation for BIM element recognition
            - Semantic segmentation mask creation
            - Automated documentation with color-coded element classification

            Outputs:
            - Normal render image (original viewport appearance)
            - Semantic color map (objects colored by element_type)
            - Color mapping info in response (color-to-element_type correspondence)
            """;

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "outputDirectory": {
                        "type": "string",
                        "description": "Directory where output files will be saved"
                    },
                    "baseName": {
                        "type": "string",
                        "description": "Base filename for outputs (e.g., 'scene01' produces scene01_render.png, scene01_semantic.png)"
                    },
                    "viewportName": {
                        "type": "string",
                        "description": "Viewport to capture. If not specified, active viewport is used."
                    },
                    "attributeKey": {
                        "type": "string",
                        "description": "User string key to use for segmentation. Defaults to 'element_type'."
                    },
                    "unassignedColor": {
                        "type": "object",
                        "description": "Color for objects without the attribute. Defaults to black (0,0,0).",
                        "properties": {
                            "r": { "type": "integer", "minimum": 0, "maximum": 255 },
                            "g": { "type": "integer", "minimum": 0, "maximum": 255 },
                            "b": { "type": "integer", "minimum": 0, "maximum": 255 }
                        }
                    },
                    "format": {
                        "type": "string",
                        "enum": ["png", "jpg"],
                        "description": "Image format. PNG recommended for semantic masks. Defaults to 'png'."
                    }
                },
                "required": ["outputDirectory", "baseName"]
            }
            """);

        public async Task<CallToolResult> ExecuteAsync(CallToolRequestParams request, McpServer? server)
        {
            try
            {
                // パラメータの取得
                var (outputDirectory, baseName, viewportName, attributeKey, unassignedColor, format) = ParseParameters(request);

                var rhinoDoc = RhinoDoc.ActiveDoc;

                // ビューポートの取得
                var view = string.IsNullOrEmpty(viewportName)
                    ? rhinoDoc.Views.ActiveView
                    : rhinoDoc.Views.Find(viewportName, false);

                if (view == null)
                {
                    throw new McpProtocolException($"Viewport not found: {viewportName ?? "active"}");
                }

                // 出力ディレクトリの作成
                if (!Directory.Exists(outputDirectory))
                {
                    Directory.CreateDirectory(outputDirectory);
                }

                // ファイルパスの生成
                var extension = format == "jpg" ? ".jpg" : ".png";
                var renderPath = Path.Combine(outputDirectory, $"{baseName}_render{extension}");
                var semanticPath = Path.Combine(outputDirectory, $"{baseName}_semantic{extension}");

                // キャプチャサイズ
                var size = new System.Drawing.Size(
                    view.ClientRectangle.Width,
                    view.ClientRectangle.Height
                );

                // 1. 通常レンダリングをキャプチャ
                await CaptureAndSaveAsync(view, size, renderPath, format);

                // 2. element_type属性を収集
                var (elementTypeGroups, unassignedObjects) = CollectElementTypes(rhinoDoc, attributeKey);

                // 3. 色マッピング生成
                var elementTypes = elementTypeGroups.Keys.ToList();
                var colorMapping = SemanticColorGenerator.GenerateColorMapping(elementTypes);

                // 4. セマンティック画像のキャプチャ
                using (var colorManager = new ObjectColorStateManager(rhinoDoc))
                {
                    // UIスレッドで色変更・Redraw・キャプチャを実行
                    Bitmap? capturedBitmap = null;

                    RhinoApp.InvokeOnUiThread(() =>
                    {
                        // 色を一時的に変更
                        foreach (var (elementType, objects) in elementTypeGroups)
                        {
                            if (colorMapping.TryGetValue(elementType, out var color))
                            {
                                foreach (var obj in objects)
                                {
                                    colorManager.SetTemporaryColor(obj, color);
                                }
                            }
                        }

                        // 未割り当てオブジェクトに色を設定
                        foreach (var obj in unassignedObjects)
                        {
                            colorManager.SetTemporaryColor(obj, unassignedColor);
                        }

                        // ビューを更新
                        rhinoDoc.Views.Redraw();

                        // キャプチャ（UIスレッド内で同期的に実行）
                        capturedBitmap = view.CaptureToBitmap(size);
                    });

                    // 画像保存は非同期で実行可能
                    if (capturedBitmap == null)
                    {
                        throw new McpProtocolException("Failed to capture semantic viewport");
                    }

                    await SaveBitmapAsync(capturedBitmap, semanticPath, format);
                    capturedBitmap.Dispose();
                } // colorManager.Dispose() で元の色に復元

                // 5. レスポンスの作成
                var renderInfo = new FileInfo(renderPath);
                var semanticInfo = new FileInfo(semanticPath);

                var response = new
                {
                    status = "success",
                    files = new
                    {
                        render = new
                        {
                            path = renderPath,
                            format,
                            width = size.Width,
                            height = size.Height,
                            sizeBytes = renderInfo.Length
                        },
                        semantic = new
                        {
                            path = semanticPath,
                            format,
                            width = size.Width,
                            height = size.Height,
                            sizeBytes = semanticInfo.Length
                        }
                    },
                    summary = new
                    {
                        totalObjects = elementTypeGroups.Values.Sum(g => g.Count) + unassignedObjects.Count,
                        assignedObjects = elementTypeGroups.Values.Sum(g => g.Count),
                        unassignedObjects = unassignedObjects.Count,
                        uniqueElementTypes = elementTypes.Count,
                        elementTypes,
                        colorMapping = colorMapping.Select(kvp => new
                        {
                            elementType = kvp.Key,
                            color = new { r = kvp.Value.R, g = kvp.Value.G, b = kvp.Value.B },
                            hexColor = SemanticColorGenerator.ToHexString(kvp.Value),
                            objectCount = elementTypeGroups.TryGetValue(kvp.Key, out var objs) ? objs.Count : 0
                        }).ToList(),
                        unassignedColorInfo = new
                        {
                            color = new { r = unassignedColor.R, g = unassignedColor.G, b = unassignedColor.B },
                            hexColor = SemanticColorGenerator.ToHexString(unassignedColor)
                        }
                    }
                };

                return new CallToolResult()
                {
                    Content =
                    [
                        new TextContentBlock()
                        {
                            Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true })
                        }
                    ]
                };
            }
            catch (Exception ex) when (ex is not McpProtocolException)
            {
                throw new McpProtocolException($"Error exporting semantic view: {ex.Message}", ex);
            }
        }

        private static (string outputDirectory, string baseName, string? viewportName, string attributeKey, System.Drawing.Color unassignedColor, string format) ParseParameters(CallToolRequestParams request)
        {
            string? outputDirectory = null;
            string? baseName = null;
            string? viewportName = null;
            string attributeKey = "element_type";
            var unassignedColor = System.Drawing.Color.Black;
            string format = "png";

            if (request.Arguments != null)
            {
                if (request.Arguments.TryGetValue("outputDirectory", out var outputDirValue))
                {
                    outputDirectory = outputDirValue.ToString();
                }
                if (request.Arguments.TryGetValue("baseName", out var baseNameValue))
                {
                    baseName = baseNameValue.ToString();
                }
                if (request.Arguments.TryGetValue("viewportName", out var viewportValue))
                {
                    viewportName = viewportValue.ToString();
                }
                if (request.Arguments.TryGetValue("attributeKey", out var attrKeyValue))
                {
                    attributeKey = attrKeyValue.ToString() ?? "element_type";
                }
                if (request.Arguments.TryGetValue("format", out var formatValue))
                {
                    var formatStr = formatValue.ToString()?.ToLowerInvariant();
                    if (formatStr == "jpg" || formatStr == "jpeg")
                    {
                        format = "jpg";
                    }
                }
                if (request.Arguments.TryGetValue("unassignedColor", out var colorValue))
                {
                    if (colorValue is JsonElement colorElement && colorElement.ValueKind == JsonValueKind.Object)
                    {
                        int r = 0, g = 0, b = 0;
                        if (colorElement.TryGetProperty("r", out var rProp)) r = rProp.GetInt32();
                        if (colorElement.TryGetProperty("g", out var gProp)) g = gProp.GetInt32();
                        if (colorElement.TryGetProperty("b", out var bProp)) b = bProp.GetInt32();
                        unassignedColor = System.Drawing.Color.FromArgb(r, g, b);
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(outputDirectory))
            {
                throw new McpProtocolException("outputDirectory is required");
            }
            if (string.IsNullOrWhiteSpace(baseName))
            {
                throw new McpProtocolException("baseName is required");
            }

            return (outputDirectory, baseName, viewportName, attributeKey, unassignedColor, format);
        }

        private static (Dictionary<string, List<RhinoObject>> elementTypeGroups, List<RhinoObject> unassignedObjects) CollectElementTypes(RhinoDoc rhinoDoc, string attributeKey)
        {
            var elementTypeGroups = new Dictionary<string, List<RhinoObject>>();
            var unassignedObjects = new List<RhinoObject>();

            foreach (var obj in rhinoDoc.Objects)
            {
                // 非表示オブジェクトはスキップ
                if (!obj.Attributes.Visible)
                {
                    continue;
                }

                var userStrings = obj.Attributes.GetUserStrings();
                var elementType = userStrings?[attributeKey];

                if (!string.IsNullOrEmpty(elementType))
                {
                    if (!elementTypeGroups.ContainsKey(elementType))
                    {
                        elementTypeGroups[elementType] = new List<RhinoObject>();
                    }
                    elementTypeGroups[elementType].Add(obj);
                }
                else
                {
                    unassignedObjects.Add(obj);
                }
            }

            return (elementTypeGroups, unassignedObjects);
        }

        private static async Task CaptureAndSaveAsync(Rhino.Display.RhinoView view, System.Drawing.Size size, string filePath, string format)
        {
            using var bitmap = view.CaptureToBitmap(size);
            if (bitmap == null)
            {
                throw new McpProtocolException("Failed to capture viewport");
            }

            await SaveBitmapAsync(bitmap, filePath, format);
        }

        /// <summary>
        /// BitmapをファイルとしてImage形式で保存する
        /// </summary>
        private static async Task SaveBitmapAsync(Bitmap bitmap, string filePath, string format)
        {
            // bitmapをMemoryStreamに保存
            using var intermediateStream = new MemoryStream();
            bitmap.Save(intermediateStream, ImageFormat.Png);
            intermediateStream.Seek(0, SeekOrigin.Begin);

            // ImageSharpを使用して画像を処理・保存
            using var image = await SixLabors.ImageSharp.Image.LoadAsync(intermediateStream);

            if (format == "jpg")
            {
                await image.SaveAsJpegAsync(filePath);
            }
            else
            {
                await image.SaveAsPngAsync(filePath);
            }
        }

    }
}
