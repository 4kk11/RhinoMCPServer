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
    /// アクティブなGrasshopperドキュメントをGHX/GHファイルとして保存する
    /// </summary>
    public class SaveGrasshopperFileTool : IMCPTool
    {
        public string Name => "save_grasshopper_file";

        public string Description => "Saves the active Grasshopper document to a GHX/GH file";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "file_path": {
                        "type": "string",
                        "description": "Absolute path to save the file (.ghx or .gh)"
                    },
                    "overwrite": {
                        "type": "boolean",
                        "description": "Whether to overwrite an existing file",
                        "default": false
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

                // overwriteパラメータ取得
                bool overwrite = false;
                if (request.Arguments.TryGetValue("overwrite", out var overwriteValue) &&
                    overwriteValue is JsonElement overwriteElement)
                {
                    overwrite = overwriteElement.GetBoolean();
                }

                // ファイル既存チェック
                if (!overwrite && File.Exists(filePath))
                {
                    throw new McpProtocolException($"File already exists: {filePath}. Set overwrite=true to replace");
                }

                // UIスレッドでGH_DocumentIOを使用して保存
                object? result = null;
                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    var doc = Instances.ActiveCanvas?.Document;
                    if (doc == null)
                    {
                        result = new { error = "No active Grasshopper document found" };
                        return;
                    }

                    // 親ディレクトリが存在しない場合は作成
                    var directory = Path.GetDirectoryName(filePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var io = new GH_DocumentIO(doc);
                    bool success = io.SaveQuiet(filePath);

                    if (!success)
                    {
                        result = new { error = $"Cannot write to path: {filePath}" };
                        return;
                    }

                    var fileInfo = new FileInfo(filePath);
                    var componentCount = doc.Objects.Count(o => o is IGH_ActiveObject);

                    result = new
                    {
                        status = "success",
                        file = new
                        {
                            path = filePath,
                            size_bytes = fileInfo.Length,
                            component_count = componentCount
                        }
                    };
                }));

                if (result is { } r)
                {
                    var jsonStr = JsonSerializer.Serialize(r, new JsonSerializerOptions { WriteIndented = true });

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
                throw new McpProtocolException($"Error saving Grasshopper file: {ex.Message}", ex);
            }
        }
    }
}
