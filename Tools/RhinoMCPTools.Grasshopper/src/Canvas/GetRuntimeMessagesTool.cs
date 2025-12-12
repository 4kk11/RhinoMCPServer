using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Grasshopper;
using Grasshopper.Kernel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Grasshopper.Canvas
{
    /// <summary>
    /// Tool to retrieve runtime messages from specified components
    /// </summary>
    public class GetRuntimeMessagesTool : IMCPTool
    {
        public string Name => "get_runtime_messages";

        public string Description => "Retrieves runtime messages from specified Grasshopper components";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>(@"{
            ""type"": ""object"",
            ""properties"": {
                ""component_ids"": {
                    ""type"": ""array"",
                    ""description"": ""List of component IDs to retrieve messages from"",
                    ""items"": {
                        ""type"": ""string"",
                        ""description"": ""GUID of the component or parameter""
                    }
                }
            },
            ""required"": [""component_ids""]
        }");

        public Task<CallToolResult> ExecuteAsync(CallToolRequestParams request, McpServer? server)
        {
            try
            {
                // アクティブなGrasshopperドキュメントを取得
                var doc = Instances.ActiveCanvas?.Document;
                if (doc == null)
                {
                    throw new McpProtocolException("No active Grasshopper document found");
                }

                // コンポーネントIDの配列を取得
                if (request.Arguments == null || !request.Arguments.TryGetValue("component_ids", out var componentsValue))
                {
                    throw new McpProtocolException("component_ids parameter is required");
                }

                var componentsArray = (JsonElement)componentsValue;
                if (componentsArray.ValueKind != JsonValueKind.Array)
                {
                    throw new McpProtocolException("component_ids parameter must be an array of strings");
                }

                // 各コンポーネントのメッセージを取得
                var messages = componentsArray.EnumerateArray()
                    .Select(element => element.GetString())
                    .Where(id => !string.IsNullOrEmpty(id))
                    .Select(id =>
                    {
                        if (!Guid.TryParse(id, out var guid))
                        {
                            return null;
                        }

                        var obj = doc.FindObject(guid, false);
                        if (obj == null)
                        {
                            return null;
                        }

                        var runtimeMessages = new System.Collections.Generic.List<object>();

                        // IGH_ActiveObjectからメッセージを取得
                        if (obj is IGH_ActiveObject activeObj)
                        {
                            runtimeMessages.AddRange(activeObj.RuntimeMessages(GH_RuntimeMessageLevel.Error)
                                .Select(msg => new { level = "error", message = msg }));
                            runtimeMessages.AddRange(activeObj.RuntimeMessages(GH_RuntimeMessageLevel.Warning)
                                .Select(msg => new { level = "warning", message = msg }));
                            runtimeMessages.AddRange(activeObj.RuntimeMessages(GH_RuntimeMessageLevel.Remark)
                                .Select(msg => new { level = "remark", message = msg }));
                        }

                        return new
                        {
                            id,
                            name = obj.Name,
                            nickname = obj.NickName,
                            messages = runtimeMessages.ToArray()
                        };
                    })
                    .Where(result => result != null)
                    .ToArray();

                // レスポンスを構築
                var response = new
                {
                    status = "success",
                    results = messages
                };

                return Task.FromResult(new CallToolResult()
                {
                    Content = [new TextContentBlock()
                    {
                        Text = JsonSerializer.Serialize(response, new JsonSerializerOptions
                        {
                            WriteIndented = true
                        }),
                    }]
                });
            }
            catch (Exception ex)
            {
                throw new McpProtocolException($"Error getting runtime messages: {ex.Message}", ex);
            }
        }
    }
}