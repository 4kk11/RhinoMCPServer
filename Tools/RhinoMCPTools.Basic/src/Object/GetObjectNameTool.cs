using System;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using System.Text.Json;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Basic
{
    public class GetObjectNameTool : IMCPTool
    {
        public string Name => "get_object_name";
        public string Description => "Gets the name of a Rhino object.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "guid": {
                        "type": "string",
                        "description": "The GUID of the target Rhino object."
                    }
                },
                "required": ["guid"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("guid", out var guidValue))
            {
                throw new McpServerException("Missing required argument: 'guid' is required");
            }

            var rhinoDoc = RhinoDoc.ActiveDoc;
            if (!Guid.TryParse(guidValue.ToString(), out Guid objectGuid))
            {
                throw new McpServerException("Invalid GUID format");
            }

            var rhinoObject = rhinoDoc.Objects.Find(objectGuid);
            if (rhinoObject == null)
            {
                throw new McpServerException($"No object found with GUID: {objectGuid}");
            }

            var response = new
            {
                status = "success",
                data = new
                {
                    guid = objectGuid.ToString(),
                    name = rhinoObject.Attributes.Name ?? string.Empty
                }
            };

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }), Type = "text" }]
            });
        }
    }
}