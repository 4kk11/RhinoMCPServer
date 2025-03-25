using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using Rhino.Geometry;
using System.Text.Json;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Basic
{
    public class MoveObjectsTool : IMCPTool
    {
        public string Name => "move_objects";
        public string Description => "Moves specified Rhino objects by a vector.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "guids": {
                        "type": "array",
                        "items": {
                            "type": "string"
                        },
                        "description": "Array of GUIDs of the objects to move."
                    },
                    "vector": {
                        "type": "object",
                        "properties": {
                            "x": {
                                "type": "number",
                                "description": "X component of the movement vector"
                            },
                            "y": {
                                "type": "number",
                                "description": "Y component of the movement vector"
                            },
                            "z": {
                                "type": "number",
                                "description": "Z component of the movement vector",
                                "default": 0
                            }
                        },
                        "required": ["x", "y"],
                        "description": "Vector defining the movement direction and distance"
                    }
                },
                "required": ["guids", "vector"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("guids", out var guidsValue) ||
                !request.Arguments.TryGetValue("vector", out var vectorValue))
            {
                throw new McpServerException("Missing required arguments: 'guids' and 'vector' are required");
            }

            var jsonElement = (JsonElement)guidsValue;
            if (jsonElement.ValueKind != JsonValueKind.Array)
            {
                throw new McpServerException("The 'guids' argument must be an array");
            }

            var vectorElement = (JsonElement)vectorValue;
            double vectorX = vectorElement.GetProperty("x").GetDouble();
            double vectorY = vectorElement.GetProperty("y").GetDouble();
            double vectorZ = vectorElement.TryGetProperty("z", out var vectorZElement) ? vectorZElement.GetDouble() : 0.0;

            var moveVector = new Vector3d(vectorX, vectorY, vectorZ);
            if (moveVector.Length < double.Epsilon)
            {
                throw new McpServerException("Movement vector length cannot be zero");
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

            var transform = Transform.Translation(moveVector);

            foreach (var guidString in guidStrings)
            {
                if (Guid.TryParse(guidString, out var guid))
                {
                    var obj = rhinoDoc.Objects.Find(guid);
                    if (obj != null)
                    {
                        if (obj.Geometry != null)
                        {
                            RhinoApp.InvokeOnUiThread(() =>
                            {
                                if (obj.Geometry.Transform(transform))
                                {
                                    obj.CommitChanges();
                                    successCount++;
                                    results.Add(new { guid = guidString, status = "success" });
                                }
                                else
                                {
                                    failureCount++;
                                    results.Add(new { guid = guidString, status = "failure", reason = "Transform operation failed" });
                                }
                            });
                        }
                        else
                        {
                            failureCount++;
                            results.Add(new { guid = guidString, status = "failure", reason = "Object has no geometry" });
                        }
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
                    successfulMoves = successCount,
                    failedMoves = failureCount,
                    vector = new { x = vectorX, y = vectorY, z = vectorZ }
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