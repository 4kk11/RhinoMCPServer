using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Rhino;
using RhinoMCPServer.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace RhinoMCPTools.Basic
{
    public class DeleteObjectsTool : IMCPTool
    {
        public string Name => "delete_objects";
        public string Description => "Deletes multiple Rhino objects by their GUIDs.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "guids": {
                        "type": "array",
                        "items": {
                            "type": "string"
                        },
                        "description": "Array of GUIDs of the objects to delete."
                    }
                },
                "required": ["guids"]
            }
            """);

        public Task<CallToolResult> ExecuteAsync(CallToolRequestParams request, McpServer? server)
        {
            if (request.Arguments is null || !request.Arguments.TryGetValue("guids", out var guidsValue))
            {
                throw new McpProtocolException("Missing required argument 'guids'");
            }

            var jsonElement = (JsonElement)guidsValue;
            if (jsonElement.ValueKind != JsonValueKind.Array)
            {
                throw new McpProtocolException("The 'guids' argument must be an array");
            }

            var guidStrings = jsonElement.EnumerateArray()
                .Select(x => x.GetString())
                .Where(x => x != null)
                .ToList();

            if (!guidStrings.Any())
            {
                throw new McpProtocolException("The guids array cannot be empty");
            }

            var rhinoDoc = RhinoDoc.ActiveDoc;
            var results = new List<object>();
            var successCount = 0;
            var failureCount = 0;

            foreach (var guidString in guidStrings)
            {
                if (Guid.TryParse(guidString, out var guid))
                {
                    var success = rhinoDoc.Objects.Delete(guid, true);
                    if (success)
                    {
                        successCount++;
                        results.Add(new { guid = guidString, status = "success" });
                    }
                    else
                    {
                        failureCount++;
                        results.Add(new { guid = guidString, status = "failure", reason = "Object not found or cannot be deleted" });
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
                    successfulDeletes = successCount,
                    failedDeletes = failureCount
                },
                results = results
            };

            return Task.FromResult(new CallToolResult()
            {
                Content = [new TextContentBlock() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }) }]
            });
        }
    }
}