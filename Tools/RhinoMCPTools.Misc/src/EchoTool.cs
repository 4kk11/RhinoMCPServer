using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using RhinoMCPServer.Common;
using System.Text.Json;
using System.Threading.Tasks;

namespace RhinoMCPServer.MCP.Tools
{
    public class EchoTool : IMCPTool
    {
        public string Name => "echo";
        public string Description => "Echoes the input back to the client.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "message": {
                        "type": "string",
                        "description": "The input to echo back."
                    }
                },
                "required": ["message"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null || !request.Arguments.TryGetValue("message", out var message))
            {
                throw new McpServerException("Missing required argument 'message'");
            }

            RhinoApp.WriteLine("Echo: " + message.ToString());

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() { Text = "Echo: " + message.ToString(), Type = "text" }]
            });
        }
    }
}