using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using RhinoMCPServer.Common;
using SixLabors.ImageSharp;
using System.Drawing.Imaging;
using System.IO;
using System.Text.Json;

namespace RhinoMCPTools.Basic
{
    public class CaptureViewportTool : IMCPTool
    {
        public string Name => "capture_viewport";
        public string Description => """
            Captures the specified Rhino viewport as an image for various purposes:
            • Documentation: Records the current state of 3D workspace
            • Communication: Facilitates design reviews and progress sharing
            • Debugging: Helps verify object positions and relationships
            
            Note: For capturing scenes with object GUIDs, use 'create_guid_text_dots' tool first to visualize the GUIDs in the viewport.
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
                    }
                }
            }
            """);

        public async Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            var rhinoDoc = RhinoDoc.ActiveDoc;
            var activeView = rhinoDoc.Views.ActiveView;

            // パラメータの取得
            string? viewportName = null;
            int? width = null;
            int? height = null;
            string format = "png";

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
            }

            // ビューポートの取得
            var view = string.IsNullOrEmpty(viewportName)
                ? activeView
                : rhinoDoc.Views.Find(viewportName, false);

            if (view == null)
            {
                throw new McpServerException($"Viewport not found: {viewportName ?? "active"}");
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

            // bitmapをMemoryStreamに保存 (PNGフォーマット)
            using var intermediateStream = new MemoryStream();
            bitmap.Save(intermediateStream, ImageFormat.Png);
            intermediateStream.Seek(0, SeekOrigin.Begin);

            // ImageSharp.Imageとして読み込む
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

            return new CallToolResponse()
            {
                Content = [new Content()
                {
                    Type = "image",
                    Data = base64Image,
                    MimeType = format == "jpg" ? "image/jpeg" : "image/png"
                }]
            };
        }
    }
}