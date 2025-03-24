using System;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using Rhino.Geometry;
using System.Text.Json;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Basic
{
    [MCPToolPlugin("sphere")]
    public class SphereTool : IMCPTool
    {
        public string Name => "sphere";
        public string Description => "Creates a sphere in Rhino.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "radius": {
                        "type": "number",
                        "description": "The radius of the sphere."
                    },
                    "x": {
                        "type": "number",
                        "description": "The x-coordinate of the sphere center.",
                        "default": 0
                    },
                    "y": {
                        "type": "number",
                        "description": "The y-coordinate of the sphere center.",
                        "default": 0
                    },
                    "z": {
                        "type": "number",
                        "description": "The z-coordinate of the sphere center.",
                        "default": 0
                    }
                },
                "required": ["radius"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null || !request.Arguments.TryGetValue("radius", out var radius))
            {
                throw new McpServerException("Missing required argument 'radius'");
            }

            var x = request.Arguments.TryGetValue("x", out var xValue) ? Convert.ToDouble(xValue.ToString()) : 0.0;
            var y = request.Arguments.TryGetValue("y", out var yValue) ? Convert.ToDouble(yValue.ToString()) : 0.0;
            var z = request.Arguments.TryGetValue("z", out var zValue) ? Convert.ToDouble(zValue.ToString()) : 0.0;

            var center = new Point3d(x, y, z);
            var rhinoDoc = RhinoDoc.ActiveDoc;
            var sphereGeometry = new Rhino.Geometry.Sphere(center, Convert.ToDouble(radius?.ToString()));
            var guid = rhinoDoc.Objects.AddSphere(sphereGeometry, null);
            rhinoDoc.Views.Redraw();

            var response = new
            {
                status = "success",
                sphere = new
                {
                    radius = Convert.ToDouble(radius?.ToString()),
                    guid = guid.ToString(),
                    center = new
                    {
                        x = x,
                        y = y,
                        z = z
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