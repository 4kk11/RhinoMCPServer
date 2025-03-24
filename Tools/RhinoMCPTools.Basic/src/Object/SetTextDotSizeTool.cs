using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using RhinoMCPServer.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Rhino.DocObjects;
using Rhino.Geometry;

namespace RhinoMCPTools.Basic
{
    public class SetTextDotSizeTool : IMCPTool
    {
        public string Name => "set_text_dot_size";
        public string Description => "Sets the font height of text dots.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "guids": {
                        "type": "array",
                        "items": {
                            "type": "string"
                        },
                        "description": "Array of GUIDs of the text dots to modify."
                    },
                    "font_height": {
                        "type": "number",
                        "description": "New font height for the text dots.",
                        "minimum": 1
                    }
                },
                "required": ["guids", "font_height"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null || 
                !request.Arguments.TryGetValue("guids", out var guidsValue) ||
                !request.Arguments.TryGetValue("font_height", out var fontHeightValue))
            {
                throw new McpServerException("Missing required arguments: 'guids' and 'font_height' are required");
            }

            var jsonElement = (JsonElement)guidsValue;
            if (jsonElement.ValueKind != JsonValueKind.Array)
            {
                throw new McpServerException("The 'guids' argument must be an array");
            }

            var fontHeight = Convert.ToDouble(fontHeightValue.ToString());
            if (fontHeight < 1)
            {
                throw new McpServerException("Font height must be greater than or equal to 1");
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
                    if (obj != null && obj is TextDotObject textDotObj)
                    {
                        var textDot = textDotObj.Geometry as TextDot;
                        if (textDot != null)
                        {
                            RhinoApp.InvokeOnUiThread(() =>
                            {
                                textDot.FontHeight = (int)fontHeight;
                                obj.CommitChanges();
                            });
                            successCount++;
                            results.Add(new { guid = guidString, status = "success" });
                        }
                        else
                        {
                            failureCount++;
                            results.Add(new { guid = guidString, status = "failure", reason = "Invalid text dot geometry" });
                        }
                    }
                    else
                    {
                        failureCount++;
                        results.Add(new { guid = guidString, status = "failure", reason = "Object not found or not a text dot" });
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
                    newFontHeight = fontHeight
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