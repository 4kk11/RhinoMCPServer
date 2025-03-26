using System;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using System.Text.Json;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Basic
{
    public class SetObjectNameTool : IMCPTool
    {
        public string Name => "set_object_name";
        public string Description => "Sets the name of a Rhino object.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "guid": {
                        "type": "string",
                        "description": "The GUID of the target Rhino object."
                    },
                    "name": {
                        "type": "string",
                        "description": "The name to set for the object."
                    }
                },
                "required": ["guid", "name"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("guid", out var guidValue) ||
                !request.Arguments.TryGetValue("name", out var nameValue))
            {
                throw new McpServerException("Missing required arguments: 'guid' and 'name' are required");
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

            rhinoObject.Attributes.Name = nameValue.ToString();
            rhinoObject.CommitChanges();
            rhinoDoc.Views.Redraw();

            var response = new
            {
                status = "success",
                data = new
                {
                    guid = objectGuid.ToString(),
                    name = nameValue.ToString()
                }
            };

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }), Type = "text" }]
            });
        }
    }
}