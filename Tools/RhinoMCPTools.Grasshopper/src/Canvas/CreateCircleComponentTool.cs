using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Grasshopper;
using Grasshopper.Kernel;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using RhinoMCPServer.Common;
using CurveComponents;
using Rhino;

namespace RhinoMCPTools.Grasshopper
{
    public class CreateCircleComponentTool : IMCPTool
    {
        public string Name => "create_circle_component";
        public string Description => "Creates a Circle component on the Grasshopper canvas at the specified position.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "x": {
                        "type": "number",
                        "description": "The x-coordinate position on the canvas",
                        "default": 0
                    },
                    "y": {
                        "type": "number",
                        "description": "The y-coordinate position on the canvas",
                        "default": 0
                    }
                }
            }
            """);

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            try
            {
                // Get position parameters
                var x = request.Arguments?.TryGetValue("x", out var xValue) == true ? 
                    Convert.ToDouble(xValue.ToString()) : 0.0;
                var y = request.Arguments?.TryGetValue("y", out var yValue) == true ? 
                    Convert.ToDouble(yValue.ToString()) : 0.0;

                // Get active Grasshopper document
                GH_Document? doc = Instances.ActiveDocument;
                if (doc == null)
                {
                    throw new McpServerException("No active Grasshopper document found");
                }
                

                // Create and add circle component
                var component = new Component_Circle();
                // Set component position
                
                doc.AddObject(component, false);
                component.Attributes.Pivot = new System.Drawing.PointF((float)x, (float)y);

                // Force complete solution recalculation to update UI
                component.ExpireSolution(true);
                RhinoApp.InvokeOnUiThread(() =>
                {
                    Instances.RedrawCanvas();
                });

                var response = new
                {
                    status = "success",
                    component = new
                    {
                        guid = component.InstanceGuid.ToString(),
                        name = component.Name,
                        position = new
                        {
                            x = x,
                            y = y
                        }
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
                throw new McpServerException($"Error creating circle component: {ex.Message}", ex);
            }
        }
    }
}