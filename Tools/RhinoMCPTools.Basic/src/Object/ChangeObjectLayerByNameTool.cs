using System;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using System.Text.Json;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Basic
{
    public class ChangeObjectLayerByNameTool : IMCPTool
    {
        public string Name => "change_object_layer_by_name";
        public string Description => "Changes the layer of a Rhino object using layer name.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "guid": {
                        "type": "string",
                        "description": "The GUID of the target Rhino object."
                    },
                    "layer_name": {
                        "type": "string",
                        "description": "The name of the target layer."
                    }
                },
                "required": ["guid", "layer_name"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("guid", out var guidValue) ||
                !request.Arguments.TryGetValue("layer_name", out var layerNameValue))
            {
                throw new McpServerException("Missing required arguments: 'guid' and 'layer_name' are required");
            }

            var rhinoDoc = RhinoDoc.ActiveDoc;

            // オブジェクトのGUIDを解析
            if (!Guid.TryParse(guidValue.ToString(), out Guid objectGuid))
            {
                throw new McpServerException("Invalid GUID format");
            }

            // オブジェクトを取得
            var obj = rhinoDoc.Objects.Find(objectGuid);
            if (obj == null)
            {
                throw new McpServerException($"No object found with GUID: {objectGuid}");
            }

            // レイヤーを取得
            var layerName = layerNameValue.ToString();
            var layerIndex = rhinoDoc.Layers.FindByFullPath(layerName, RhinoMath.UnsetIntIndex);
            if (layerIndex == -1)
            {
                throw new McpServerException($"Layer '{layerName}' not found");
            }

            // レイヤーの変更
            obj.Attributes.LayerIndex = layerIndex;
            obj.CommitChanges();
            rhinoDoc.Views.Redraw();

            var response = new
            {
                status = "success",
                data = new
                {
                    guid = objectGuid.ToString(),
                    new_layer = new
                    {
                        name = rhinoDoc.Layers[layerIndex].Name,
                        index = layerIndex
                    }
                }
            };

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }), Type = "text" }]
            });
        }
    }
}