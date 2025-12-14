using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Rhino;
using System.Text.Json;
using RhinoMCPServer.Common;
using System.Linq;
using System.Collections.Generic;

namespace RhinoMCPTools.Basic
{
    public class ListLayersTool : IMCPTool
    {
        public string Name => "list_layers";
        public string Description => "Returns a list of all layer full paths in Rhino document.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {},
                "required": []
            }
            """);

        public Task<CallToolResult> ExecuteAsync(CallToolRequestParams request, McpServer? server)
        {
            var rhinoDoc = RhinoDoc.ActiveDoc;
            var layers = rhinoDoc.Layers;
            var layerInfoList = new List<object>();

            // すべてのレイヤーをループして情報を収集
            for (var i = 0; i < layers.Count; i++)
            {
                var layer = layers[i];
                var fullPath = layer.FullPath;

                layerInfoList.Add(new
                {
                    full_path = fullPath,
                    index = i,
                    id = layer.Id,
                    color = $"#{layer.Color.R:X2}{layer.Color.G:X2}{layer.Color.B:X2}",
                    visible = layer.IsVisible,
                    locked = layer.IsLocked
                });
            }

            var response = new
            {
                status = "success",
                layers = layerInfoList
            };

            return Task.FromResult(new CallToolResult()
            {
                Content = [new TextContentBlock() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }) }]
            });
        }
    }
}