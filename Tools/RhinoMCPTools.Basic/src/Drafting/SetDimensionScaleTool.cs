using System;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using Rhino.DocObjects;
using Rhino.Geometry;
using System.Text.Json;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Basic
{
    public class SetDimensionScaleTool : IMCPTool
    {
        public string Name => "set_dimension_scale";
        public string Description => "Sets the dimension scale of a dimension object in Rhino.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "guids": {
                        "type": "array",
                        "items": {
                            "type": "string"
                        },
                        "description": "Array of GUIDs of the dimension objects to modify."
                    },
                    "scale": {
                        "type": "number",
                        "description": "The new dimension scale value.",
                        "minimum": 0
                    }
                },
                "required": ["guids", "scale"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (request.Arguments is null ||
                !request.Arguments.TryGetValue("guids", out var guidsValue) ||
                !request.Arguments.TryGetValue("scale", out var scaleValue))
            {
                throw new McpServerException("Missing required arguments: 'guids' and 'scale' are required");
            }

            var jsonElement = (JsonElement)guidsValue;
            if (jsonElement.ValueKind != JsonValueKind.Array)
            {
                throw new McpServerException("The 'guids' argument must be an array");
            }

            var newScale = Convert.ToDouble(scaleValue.ToString());
            if (newScale <= 0)
            {
                throw new McpServerException("Dimension scale must be greater than 0");
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
            var results = new List<object>();
            var successCount = 0;
            var failureCount = 0;

            foreach (var guidString in guidStrings)
            {
                if (Guid.TryParse(guidString, out var guid))
                {
                    var obj = rhinoDoc.Objects.Find(guid);
                    if (obj != null && obj.Geometry is Dimension dimension)
                    {
                        RhinoApp.InvokeOnUiThread(() =>
                        {
                            dimension.DimensionScale = newScale;
                            obj.CommitChanges();
                        });
                        successCount++;
                        results.Add(new { guid = guidString, status = "success" });
                    }
                    else
                    {
                        failureCount++;
                        results.Add(new { guid = guidString, status = "failure", reason = "Object not found or not a dimension object" });
                    }
                }
                else
                {
                    failureCount++;
                    results.Add(new { guid = guidString, status = "failure", reason = "Invalid GUID format" });
                }
            }

            RhinoApp.InvokeOnUiThread(() =>
            {
                rhinoDoc.Views.Redraw();
            });

            var response = new
            {
                summary = new
                {
                    totalObjects = guidStrings.Count,
                    successfulUpdates = successCount,
                    failedUpdates = failureCount,
                    newDimensionScale = newScale
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