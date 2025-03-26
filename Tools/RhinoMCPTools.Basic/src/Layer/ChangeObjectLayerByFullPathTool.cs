using System;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using System.Text.Json;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Basic
{
    public class ChangeObjectLayerByFullPathTool : IMCPTool
    {
        public string Name => "change_object_layer_by_full_path";
        public string Description => "Changes the layer of a Rhino object using the layer's full path.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "guid": {
                        "type": "string",
                        "description": "The GUID of the target Rhino object."
                    },
                    "layer_full_path": {
                        "type": "string",
                        "description": "The full path of the target layer (e.g. 'Parent::Child::Grandchild')."
                    }
                },
                "required": ["guid", "layer_full_path"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("guid", out var guidValue) ||
                !request.Arguments.TryGetValue("layer_full_path", out var layerPathValue))
            {
                throw new McpServerException("Missing required arguments: 'guid' and 'layer_full_path' are required");
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
            var layerPath = layerPathValue.ToString();
            var layerIndex = rhinoDoc.Layers.FindByFullPath(layerPath, RhinoMath.UnsetIntIndex);
            if (layerIndex == RhinoMath.UnsetIntIndex)
            {
                throw new McpServerException($"Layer '{layerPath}' not found");
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
                        full_path = layerPath,
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