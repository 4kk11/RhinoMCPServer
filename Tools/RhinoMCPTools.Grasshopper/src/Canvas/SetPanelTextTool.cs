using System;
using System.Threading.Tasks;
using System.Text.Json;
using Grasshopper;
using Grasshopper.Kernel;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using RhinoMCPServer.Common;
using Rhino;
using Grasshopper.Kernel.Special;

namespace RhinoMCPTools.Grasshopper
{
    public class SetPanelTextTool : IMCPTool
    {
        public string Name => "set_panel_text";
        public string Description => "Sets the text of a Panel component in the Grasshopper canvas.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "panel_id": {
                        "type": "string",
                        "description": "The GUID of the Panel component"
                    },
                    "text": {
                        "type": "string",
                        "description": "The text to set in the Panel"
                    }
                },
                "required": ["panel_id", "text"]
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            try
            {
                // Validate request parameters
                if (request.Arguments is null)
                {
                    throw new McpServerException("Missing required arguments");
                }

                if (!request.Arguments.TryGetValue("panel_id", out var panelValue) ||
                    !request.Arguments.TryGetValue("text", out var textValue))
                {
                    throw new McpServerException("Missing required parameters: panel_id and/or text");
                }

                if (!Guid.TryParse(panelValue.ToString(), out var panelGuid))
                {
                    throw new McpServerException("Invalid GUID format for panel_id");
                }

                string text = textValue.ToString() ?? string.Empty;

                // Get active Grasshopper document
                GH_Document? doc = Instances.ActiveDocument;
                if (doc == null)
                {
                    throw new McpServerException("No active Grasshopper document found");
                }

                // Find the panel component
                var obj = doc.FindObject(panelGuid, false);
                if (obj == null)
                {
                    throw new McpServerException($"Panel with GUID {panelGuid} not found");
                }

                // Validate that the object is a panel
                if (!(obj is GH_Panel panel))
                {
                    throw new McpServerException($"Object with GUID {panelGuid} is not a Panel component");
                }

                // Store panel info for response
                var panelInfo = new
                {
                    guid = panel.InstanceGuid.ToString(),
                    name = panel.Name,
                    position = new
                    {
                        x = panel.Attributes.Pivot.X,
                        y = panel.Attributes.Pivot.Y
                    }
                };

                // Set the panel text
                panel.UserText = text;

                // Force solution update and redraw
                panel.ExpireSolution(true);
                doc.NewSolution(false);
                RhinoApp.InvokeOnUiThread(() =>
                {
                    Instances.RedrawCanvas();
                });

                var response = new
                {
                    status = "success",
                    panel = new
                    {
                        info = panelInfo,
                        text = text
                    }
                };

                return Task.FromResult(new CallToolResponse()
                {
                    Content = [new Content() 
                    { 
                        Text = JsonSerializer.Serialize(response, new JsonSerializerOptions 
                        { 
                            WriteIndented = true 
                        }), 
                        Type = "text" 
                    }]
                });
            }
            catch (Exception ex)
            {
                throw new McpServerException($"Error setting panel text: {ex.Message}", ex);
            }
        }
    }
}