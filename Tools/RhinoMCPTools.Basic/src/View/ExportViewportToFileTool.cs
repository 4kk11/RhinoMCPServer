using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Rhino;
using RhinoMCPServer.Common;
using SixLabors.ImageSharp;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;
using System;

namespace RhinoMCPTools.Basic
{
    /// <summary>
    /// ビューポートをキャプチャしてローカルファイルに保存するツール
    /// </summary>
    public class ExportViewportToFileTool : IMCPTool
    {
        public string Name => "export_viewport_to_file";

        public string Description => """
            Exports the specified Rhino viewport as an image file to the local filesystem.

            Use cases:
            • Saving high-resolution viewport captures for documentation
            • Creating presentation materials
            • Archiving project snapshots

            Supported formats: PNG, JPG (determined by file extension)
            """;

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "filePath": {
                        "type": "string",
                        "description": "The full path where the image will be saved. The file format is determined by the extension (.png, .jpg, .jpeg)."
                    },
                    "viewportName": {
                        "type": "string",
                        "description": "The name of the viewport to capture. If not specified, the active viewport will be used."
                    }
                },
                "required": ["filePath"]
            }
            """);

        public async Task<CallToolResult> ExecuteAsync(CallToolRequestParams request, McpServer? server)
        {
            try
            {
                // パラメータの取得
                string? filePath = null;
                string? viewportName = null;

                if (request.Arguments != null)
                {
                    if (request.Arguments.TryGetValue("filePath", out var filePathValue))
                    {
                        filePath = filePathValue.ToString();
                    }
                    if (request.Arguments.TryGetValue("viewportName", out var viewportValue))
                    {
                        viewportName = viewportValue.ToString();
                    }
                }

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    throw new McpProtocolException("filePath is required");
                }

                // 拡張子からフォーマットを判定
                var extension = Path.GetExtension(filePath).ToLowerInvariant();
                var format = extension switch
                {
                    ".png" => "png",
                    ".jpg" or ".jpeg" => "jpg",
                    _ => throw new McpProtocolException($"Unsupported file format: {extension}. Supported formats: .png, .jpg, .jpeg")
                };

                var rhinoDoc = RhinoDoc.ActiveDoc;
                var activeView = rhinoDoc.Views.ActiveView;

                // ビューポートの取得
                var view = string.IsNullOrEmpty(viewportName)
                    ? activeView
                    : rhinoDoc.Views.Find(viewportName, false);

                if (view == null)
                {
                    throw new McpProtocolException($"Viewport not found: {viewportName ?? "active"}");
                }

                // キャプチャサイズはビューポートの現在サイズを使用
                var size = new System.Drawing.Size(
                    view.ClientRectangle.Width,
                    view.ClientRectangle.Height
                );

                // ビューポートのキャプチャ
                using var bitmap = view.CaptureToBitmap(size);
                if (bitmap == null)
                {
                    throw new McpProtocolException("Failed to capture viewport");
                }

                // 親ディレクトリが存在しない場合は作成
                var directory = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

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

                // ファイルサイズを取得
                var fileInfo = new FileInfo(filePath);

                // レスポンスの作成
                var response = new
                {
                    status = "success",
                    file = new
                    {
                        path = filePath,
                        format,
                        width = size.Width,
                        height = size.Height,
                        sizeBytes = fileInfo.Length
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
                throw new McpProtocolException($"Error exporting viewport: {ex.Message}", ex);
            }
        }
    }
}
