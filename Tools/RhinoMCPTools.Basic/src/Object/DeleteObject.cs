using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using RhinoMCPServer.Common;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace RhinoMCPTools.Basic
{
    public class DeleteObjectTool : IMCPTool
    {
        public string Name => "delete_object";
        public string Description => "Deletes a Rhino object by its GUID.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "guid": {
                        "type": "string",
                        "description": "The GUID of the object to delete."
                    }
                },
                "required": ["guid"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null || !request.Arguments.TryGetValue("guid", out var guidValue))
            {
                throw new McpServerException("Missing required argument 'guid'");
            }

            var rhinoDoc = RhinoDoc.ActiveDoc;
            if (!Guid.TryParse(guidValue.ToString(), out var guid))
            {
                throw new McpServerException("Invalid GUID format");
            }

            var success = rhinoDoc.Objects.Delete(guid, true);
            rhinoDoc.Views.Redraw();

            var response = new
            {
                status = success ? "success" : "failure",
                deletedObject = new
                {
                    guid = guid.ToString()
                }
            };

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }), Type = "text" }]
            });
        }
    }
}