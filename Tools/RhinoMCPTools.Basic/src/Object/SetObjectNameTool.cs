using System;
using System.Threading.Tasks;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
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

        public Task<CallToolResult> ExecuteAsync(CallToolRequestParams request, McpServer? server)
        {
            if (request.Arguments is null)
            {
                throw new McpProtocolException("Missing required arguments");
            }

            if (!request.Arguments.TryGetValue("guid", out var guidValue) ||
                !request.Arguments.TryGetValue("name", out var nameValue))
            {
                throw new McpProtocolException("Missing required arguments: 'guid' and 'name' are required");
            }

            var rhinoDoc = RhinoDoc.ActiveDoc;
            if (!Guid.TryParse(guidValue.ToString(), out Guid objectGuid))
            {
                throw new McpProtocolException("Invalid GUID format");
            }

            var rhinoObject = rhinoDoc.Objects.Find(objectGuid);
            if (rhinoObject == null)
            {
                throw new McpProtocolException($"No object found with GUID: {objectGuid}");
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

            return Task.FromResult(new CallToolResult()
            {
                Content = [new TextContentBlock() { Text = JsonSerializer.Serialize(response, new JsonSerializerOptions { WriteIndented = true }) }]
            });
        }
    }
}