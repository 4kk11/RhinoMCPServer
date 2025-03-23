using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using Rhino.Geometry;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace RhinoMCPServer.MCP.Tools
{
    public class SphereTool : IMCPTool
    {
        public string Name => "sphere";
        public string Description => "Creates a sphere.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "radius": {
                        "type": "number",
                        "description": "The radius of the sphere."
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

            var rhinoDoc = RhinoDoc.ActiveDoc;
            rhinoDoc.Objects.AddSphere(new Sphere(Point3d.Origin, Convert.ToDouble(radius?.ToString())), null);
            rhinoDoc.Views.Redraw();

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() { Text = $"Created sphere with radius {radius}", Type = "text" }]
            });
        }
    }
}