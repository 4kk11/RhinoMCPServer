using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Grasshopper;
using Grasshopper.Kernel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Rhino;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Grasshopper.Canvas
{
    /// <summary>
    /// GHX/GHファイルをGrasshopperに読み込み、新しいドキュメントとして開く
    /// </summary>
    public class LoadGrasshopperFileTool : IMCPTool
    {
        public string Name => "load_grasshopper_file";

        public string Description => "Loads a GHX/GH file into Grasshopper as a new document and runs the solution";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "file_path": {
                        "type": "string",
                        "description": "Absolute path to the .ghx or .gh file to load"
                    }
                },
                "required": ["file_path"]
            }
            """);

        public Task<CallToolResult> ExecuteAsync(CallToolRequestParams request, McpServer? server)
        {
            try
            {
                if (request.Arguments == null || !request.Arguments.TryGetValue("file_path", out var filePathValue))
                {
                    throw new McpProtocolException("file_path parameter is required");
                }

                var filePath = filePathValue.ToString()?.Trim('"');
                if (string.IsNullOrEmpty(filePath))
                {
                    throw new McpProtocolException("file_path parameter must not be empty");
                }

                // 拡張子チェック
                var ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext != ".ghx" && ext != ".gh")
                {
                    throw new McpProtocolException("Invalid file extension. Expected .ghx or .gh");
                }

                // ファイル存在確認
                if (!File.Exists(filePath))
                {
                    throw new McpProtocolException($"File not found: {filePath}");
                }

                // UIスレッドでGH_DocumentIOを使用してファイルを読み込む
                object? result = null;
                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    var io = new GH_DocumentIO();
                    bool success = io.Open(filePath);

                    if (!success || io.Document == null)
                    {
                        result = new { error = $"Failed to parse Grasshopper file: {filePath}" };
                        return;
                    }

                    var doc = io.Document;

                    // ドキュメントサーバーに登録
                    var docServer = Instances.DocumentServer;
                    docServer?.AddDocument(doc);

                    // ソリューション実行
                    doc.NewSolution(false);

                    // アクティブドキュメントとして設定・キャンバス再描画
                    doc.Enabled = true;
                    Instances.RedrawCanvas();

                    // IOメッセージの収集
                    var ioMessages = new System.Collections.Generic.List<string>();

                    // ドキュメント内のコンポーネントからパースエラーを収集
                    bool hasErrors = false;
                    bool hasWarnings = false;

                    foreach (var obj in doc.Objects)
                    {
                        if (obj is IGH_ActiveObject active)
                        {
                            var errors = active.RuntimeMessages(GH_RuntimeMessageLevel.Error);
                            var warnings = active.RuntimeMessages(GH_RuntimeMessageLevel.Warning);
                            if (errors.Count > 0) hasErrors = true;
                            if (warnings.Count > 0) hasWarnings = true;
                        }
                    }

                    var componentCount = doc.Objects.Count(o => o is IGH_ActiveObject);

                    result = new
                    {
                        status = "success",
                        document = new
                        {
                            file_path = filePath,
                            component_count = componentCount,
                            has_errors = hasErrors,
                            has_warnings = hasWarnings,
                            io_messages = ioMessages.ToArray()
                        }
                    };
                }));

                // UIスレッドでエラーが発生した場合
                if (result is { } r)
                {
                    var jsonStr = JsonSerializer.Serialize(r, new JsonSerializerOptions { WriteIndented = true });

                    // エラーオブジェクトかチェック
                    var jsonDoc = JsonSerializer.Deserialize<JsonElement>(jsonStr);
                    if (jsonDoc.TryGetProperty("error", out var errorProp))
                    {
                        throw new McpProtocolException(errorProp.GetString() ?? "Unknown error");
                    }

                    return Task.FromResult(new CallToolResult()
                    {
                        Content = [new TextContentBlock()
                        {
                            Text = jsonStr,
                        }]
                    });
                }

                throw new McpProtocolException("Unexpected error: no result from UI thread operation");
            }
            catch (McpProtocolException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new McpProtocolException($"Error loading Grasshopper file: {ex.Message}", ex);
            }
        }
    }
}
