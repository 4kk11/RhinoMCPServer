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
    public class ConfigureNumberSliderTool : IMCPTool
    {
        public string Name => "configure_number_slider";
        public string Description => "Configures a Number Slider component in the Grasshopper canvas by setting its value and optionally its range.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "slider_id": {
                        "type": "string",
                        "description": "The GUID of the Number Slider component"
                    },
                    "value": {
                        "type": "number",
                        "description": "The value to set in the slider"
                    },
                    "minimum": {
                        "type": "number",
                        "description": "Optional: The minimum value for the slider range"
                    },
                    "maximum": {
                        "type": "number",
                        "description": "Optional: The maximum value for the slider range"
                    }
                },
                "required": ["slider_id", "value"]
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

                if (!request.Arguments.TryGetValue("slider_id", out var sliderValue) ||
                    !request.Arguments.TryGetValue("value", out var valueValue))
                {
                    throw new McpServerException("Missing required parameters: slider_id and/or value");
                }

                if (!Guid.TryParse(sliderValue.ToString(), out var sliderGuid))
                {
                    throw new McpServerException("Invalid GUID format for slider_id");
                }

                if (!decimal.TryParse(valueValue.ToString(), out var value))
                {
                    throw new McpServerException("Invalid number format for value");
                }

                // Parse optional range parameters
                decimal? minimum = null;
                decimal? maximum = null;

                if (request.Arguments.TryGetValue("minimum", out var minValue))
                {
                    if (!decimal.TryParse(minValue.ToString(), out var min))
                    {
                        throw new McpServerException("Invalid number format for minimum");
                    }
                    minimum = min;
                }

                if (request.Arguments.TryGetValue("maximum", out var maxValue))
                {
                    if (!decimal.TryParse(maxValue.ToString(), out var max))
                    {
                        throw new McpServerException("Invalid number format for maximum");
                    }
                    maximum = max;
                }

                // Get active Grasshopper document
                GH_Document? doc = Instances.ActiveDocument;
                if (doc == null)
                {
                    throw new McpServerException("No active Grasshopper document found");
                }

                // Find the slider component
                var obj = doc.FindObject(sliderGuid, false);
                if (obj == null)
                {
                    throw new McpServerException($"Slider with GUID {sliderGuid} not found");
                }

                // Validate that the object is a number slider
                if (!(obj is GH_NumberSlider slider))
                {
                    throw new McpServerException($"Object with GUID {sliderGuid} is not a Number Slider component");
                }

                // Update slider range if provided
                if (minimum.HasValue || maximum.HasValue)
                {
                    decimal newMin = minimum ?? slider.Slider.Minimum;
                    decimal newMax = maximum ?? slider.Slider.Maximum;

                    if (newMin >= newMax)
                    {
                        throw new McpServerException($"Invalid range: minimum ({newMin}) must be less than maximum ({newMax})");
                    }

                    slider.Slider.Minimum = newMin;
                    slider.Slider.Maximum = newMax;
                }

                // Ensure value is within the slider's range
                if (value < slider.Slider.Minimum || value > slider.Slider.Maximum)
                {
                    throw new McpServerException($"Value {value} is outside the slider's range [{slider.Slider.Minimum}, {slider.Slider.Maximum}]");
                }

                // Set the slider value
                slider.SetSliderValue(value);

                // Force solution update and redraw
                slider.ExpireSolution(true);
                doc.NewSolution(false);
                RhinoApp.InvokeOnUiThread(() =>
                {
                    Instances.RedrawCanvas();
                });

                var response = new
                {
                    status = "success",
                    slider = new
                    {
                        guid = slider.InstanceGuid.ToString(),
                        name = slider.Name,
                        position = new
                        {
                            x = slider.Attributes.Pivot.X,
                            y = slider.Attributes.Pivot.Y
                        },
                        range = new
                        {
                            minimum = slider.Slider.Minimum,
                            maximum = slider.Slider.Maximum
                        },
                        current_value = value
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
                throw new McpServerException($"Error configuring number slider: {ex.Message}", ex);
            }
        }
    }
}