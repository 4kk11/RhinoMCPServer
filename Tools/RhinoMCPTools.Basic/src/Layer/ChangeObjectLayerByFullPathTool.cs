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
        public string Description => "Changes the layer of multiple Rhino objects using the layer's full path.";
    
            public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
                {
                    "type": "object",
                    "properties": {
                        "guids": {
                            "type": "array",
                            "items": {
                                "type": "string"
                            },
                            "description": "Array of GUIDs of the target Rhino objects."
                        },
                        "layer_full_path": {
                            "type": "string",
                            "description": "The full path of the target layer (e.g. 'Parent::Child::Grandchild')."
                        }
                    },
                    "required": ["guids", "layer_full_path"]
                }
                """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("guids", out var guidsValue) ||
                !request.Arguments.TryGetValue("layer_full_path", out var layerPathValue))
            {
                throw new McpServerException("Missing required arguments: 'guids' and 'layer_full_path' are required");
            }

            var jsonElement = (JsonElement)guidsValue;
            if (jsonElement.ValueKind != JsonValueKind.Array)
            {
                throw new McpServerException("The 'guids' argument must be an array");
            }

            var guidStrings = jsonElement.EnumerateArray()
                .Select(x => x.GetString())
                .Where(x => x != null)
                .ToList();

            if (!guidStrings.Any())
            {
                throw new McpServerException("The guids array cannot be empty");
            }

            var rhinoDoc = RhinoDoc.ActiveDoc;

            // レイヤーを取得
            var layerPath = layerPathValue.ToString();
            var layerIndex = rhinoDoc.Layers.FindByFullPath(layerPath, RhinoMath.UnsetIntIndex);
            if (layerIndex == RhinoMath.UnsetIntIndex)
            {
                throw new McpServerException($"Layer '{layerPath}' not found");
            }

            var results = new List<object>();
            var successCount = 0;
            var failureCount = 0;

            foreach (var guidString in guidStrings)
            {
                if (Guid.TryParse(guidString, out var guid))
                {
                    var obj = rhinoDoc.Objects.Find(guid);
                    if (obj != null)
                    {
                        obj.Attributes.LayerIndex = layerIndex;
                        obj.CommitChanges();
                        successCount++;
                        results.Add(new { guid = guidString, status = "success", new_layer = new { full_path = layerPath, index = layerIndex } });
                    }
                    else
                    {
                        failureCount++;
                        results.Add(new { guid = guidString, status = "failure", reason = "Object not found" });
                    }
                }
                else
                {
                    failureCount++;
                    results.Add(new { guid = guidString, status = "failure", reason = "Invalid GUID format" });
                }
            }

            rhinoDoc.Views.Redraw();

            var response = new
            {
                summary = new
                {
                    totalObjects = guidStrings.Count,
                    successfulUpdates = successCount,
                    failedUpdates = failureCount,
                    targetLayerPath = layerPath,
                    targetLayerIndex = layerIndex
                },
                results = results
            };

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }), Type = "text" }]
            });
        }
    }
}