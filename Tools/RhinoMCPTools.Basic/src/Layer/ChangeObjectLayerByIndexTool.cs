using System;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using System.Text.Json;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Basic
{
    public class ChangeObjectLayerByIndexTool : IMCPTool
    {
        public string Name => "change_object_layer_by_index";
        public string Description => "Changes the layer of multiple Rhino objects using layer index.";
    
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
                        "layer_index": {
                            "type": "number",
                            "description": "The index of the target layer."
                        }
                    },
                    "required": ["guids", "layer_index"]
                }
                """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("guids", out var guidsValue) ||
                !request.Arguments.TryGetValue("layer_index", out var layerIndexValue))
            {
                throw new McpServerException("Missing required arguments: 'guids' and 'layer_index' are required");
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

            // レイヤーインデックスを解析して検証
            if (!int.TryParse(layerIndexValue.ToString(), out int layerIndex))
            {
                throw new McpServerException("Invalid layer index format");
            }

            var rhinoDoc = RhinoDoc.ActiveDoc;

            // レイヤーが存在するか確認
            if (layerIndex < 0 || layerIndex >= rhinoDoc.Layers.Count)
            {
                throw new McpServerException($"Layer index {layerIndex} is out of range");
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
                        results.Add(new { guid = guidString, status = "success", new_layer = new { name = rhinoDoc.Layers[layerIndex].Name, index = layerIndex } });
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
                    targetLayerIndex = layerIndex,
                    targetLayerName = rhinoDoc.Layers[layerIndex].Name
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