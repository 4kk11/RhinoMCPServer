using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Rhino;
using Rhino.Geometry;
using RhinoMCPServer.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace RhinoMCPTools.Basic
{
    public class PolylineTool : IMCPTool
    {
        public string Name => "polyline";
        public string Description => "Creates a polyline from an array of points.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "points": {
                        "type": "array",
                        "description": "Array of points that define the polyline vertices.",
                        "items": {
                            "type": "object",
                            "properties": {
                                "x": {
                                    "type": "number",
                                    "description": "X coordinate of the point"
                                },
                                "y": {
                                    "type": "number",
                                    "description": "Y coordinate of the point"
                                },
                                "z": {
                                    "type": "number",
                                    "description": "Z coordinate of the point",
                                    "default": 0
                                }
                            },
                            "required": ["x", "y"]
                        },
                        "minItems": 2
                    }
                },
                "required": ["points"]
            }
            """);

        public Task<CallToolResult> ExecuteAsync(CallToolRequestParams request, McpServer? server)
        {
            if (request.Arguments is null || !request.Arguments.TryGetValue("points", out var pointsValue))
            {
                throw new McpProtocolException("Missing required argument 'points'");
            }

            var points = new List<Point3d>();
            var pointsElement = (JsonElement)pointsValue;
            foreach (var point in pointsElement.EnumerateArray())
            {
                var x = Convert.ToDouble(point.GetProperty("x").GetDouble());
                var y = Convert.ToDouble(point.GetProperty("y").GetDouble());
                var z = point.TryGetProperty("z", out var zElement) ? Convert.ToDouble(zElement.GetDouble()) : 0.0;
                points.Add(new Point3d(x, y, z));
            }

            if (points.Count < 2)
            {
                throw new McpProtocolException("At least 2 points are required to create a polyline");
            }

            var polyline = new Polyline(points);
            var rhinoDoc = RhinoDoc.ActiveDoc;
            var guid = rhinoDoc.Objects.AddPolyline(polyline);
            rhinoDoc.Views.Redraw();

            var response = new
            {
                status = "success",
                polyline = new
                {
                    guid = guid.ToString(),
                    points = points.Select(p => new { x = p.X, y = p.Y, z = p.Z }).ToArray()
                }
            };

            return Task.FromResult(new CallToolResult()
            {
                Content = [new TextContentBlock() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }) }]
            });
        }
    }
}