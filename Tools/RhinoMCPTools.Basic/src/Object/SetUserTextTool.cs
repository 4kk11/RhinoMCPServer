using System;
using System.Threading.Tasks;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Rhino;
using System.Text.Json;
using RhinoMCPServer.Common;

namespace RhinoMCPTools.Basic
{
    public class SetUserTextTool : IMCPTool
    {
        public string Name => "set_user_text";
        public string Description => "Sets user text attributes on a Rhino object.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "guid": {
                        "type": "string",
                        "description": "The GUID of the target Rhino object."
                    },
                    "key": {
                        "type": "string",
                        "description": "The key for the user text attribute."
                    },
                    "value": {
                        "type": "string",
                        "description": "The value to set for the user text attribute."
                    }
                },
                "required": ["guid", "key", "value"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpServerException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("guid", out var guidValue) ||
                !request.Arguments.TryGetValue("key", out var keyValue) ||
                !request.Arguments.TryGetValue("value", out var textValue))
            {
                throw new McpServerException("Missing required arguments: 'guid', 'key', and 'value' are required");
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

            rhinoObject.Attributes.SetUserString(keyValue.ToString(), textValue.ToString());
            rhinoDoc.Views.Redraw();

            var response = new
            {
                status = "success",
                data = new
                {
                    guid = objectGuid.ToString(),
                    key = keyValue.ToString(),
                    value = textValue.ToString()
                }
            };

            return Task.FromResult(new CallToolResponse()
            {
                Content = [new Content() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }), Type = "text" }]
            });
        }
    }
}