namespace RhinoMCPTools.Grasshopper.Generators.Templates
{
    internal static class ParamComponentToolTemplate
    {
        public static string GetTemplate(string namespaceName, string className, string toolClassName, string toolName)
        {
            return $@"
#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Grasshopper;
using Grasshopper.Kernel;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using RhinoMCPServer.Common;
using {namespaceName};
using Rhino;

namespace GHComponentSourceGenerator
{{
    public class {toolClassName} : IMCPTool
    {{
        public string Name => ""{toolName}"";

        public string Description {{ get; }}

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>(@""{{
            """"type"""": """"object"""",
            """"properties"""": {{
                """"x"""": {{
                    """"type"""": """"number"""",
                    """"description"""": """"The x-coordinate position on the canvas"""",
                    """"default"""": 0
                }},
                """"y"""": {{
                    """"type"""": """"number"""",
                    """"description"""": """"The y-coordinate position on the canvas"""",
                    """"default"""": 0
                }}
            }}
        }}"");

        public {toolClassName}()
        {{
            var component = new {className}();
            Description = component.Description;
        }}

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {{
            try
            {{
                // Get position parameters
                var x = request.Arguments?.TryGetValue(""x"", out var xValue) == true ?
                    Convert.ToDouble(xValue.ToString()) : 0.0;
                var y = request.Arguments?.TryGetValue(""y"", out var yValue) == true ?
                    Convert.ToDouble(yValue.ToString()) : 0.0;

                // Get active Grasshopper document
                GH_Document? doc = Instances.ActiveDocument;
                if (doc == null)
                {{
                    throw new McpServerException(""No active Grasshopper document found"");
                }}

                // Create and add component
                var component = new {className}();
                doc.AddObject(component, false);
                component.Attributes.Pivot = new System.Drawing.PointF((float)x, (float)y);

                // Force complete solution recalculation to update UI
                component.ExpireSolution(true);
                RhinoApp.InvokeOnUiThread(() =>
                {{
                    Instances.RedrawCanvas();
                }});

                var response = new
                {{
                    status = ""success"",
                    component = new
                    {{
                        param_id = component.InstanceGuid.ToString(),
                        name = component.Name,
                        nickname = component.NickName,
                        description = component.Description,
                        type_name = component.TypeName,
                        position = new
                        {{
                            x = x,
                            y = y
                        }}
                    }}
                }};

                return Task.FromResult(new CallToolResponse()
                {{
                    Content = [new Content()
                    {{
                        Text = JsonSerializer.Serialize(response, new JsonSerializerOptions
                        {{
                            WriteIndented = true
                        }}),
                        Type = ""text""
                    }}]
                }});
            }}
            catch (Exception ex)
            {{
                throw new McpServerException($""Error creating {className} parameter: {{ex.Message}}"", ex);
            }}
        }}
    }}
}}";
        }
    }
}