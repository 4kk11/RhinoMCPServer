using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using RhinoMCPServer.Common;
using Grasshopper;
using Grasshopper.Kernel;
using Rhino;
using RhinoMCPTools.Grasshopper.Analysis;

namespace RhinoMCPTools.Grasshopper.Canvas
{
    /// <summary>
    /// 指定されたGrasshopperコンポーネントを作成するツール
    /// component_guid, type_name, name のいずれかで指定可能
    /// </summary>
    public class CreateComponentTool : IMCPTool
    {
        private readonly GrasshopperComponentAnalyzer _analyzer;

        public CreateComponentTool()
        {
            _analyzer = new GrasshopperComponentAnalyzer();
        }

        public string Name => "create_component";

        public string Description => "Creates a Grasshopper component on the canvas by component_guid, type_name, or name";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "component_guid": {
                        "type": "string",
                        "description": "ComponentGUID of the component to create (highest priority)"
                    },
                    "type_name": {
                        "type": "string",
                        "description": "Fully qualified type name of the component"
                    },
                    "name": {
                        "type": "string",
                        "description": "Component name (partial match search). Error if ambiguous."
                    },
                    "x": {
                        "type": "number",
                        "description": "X position on the canvas",
                        "default": 0
                    },
                    "y": {
                        "type": "number",
                        "description": "Y position on the canvas",
                        "default": 0
                    }
                }
            }
            """);

        public Task<CallToolResult> ExecuteAsync(CallToolRequestParams request, McpServer? server)
        {
            try
            {
                if (request.Arguments == null)
                {
                    throw new McpProtocolException("One of component_guid, type_name, or name is required");
                }

                var x = request.Arguments.TryGetValue("x", out var xValue)
                    ? Convert.ToDouble(xValue.ToString())
                    : 0.0;
                var y = request.Arguments.TryGetValue("y", out var yValue)
                    ? Convert.ToDouble(yValue.ToString())
                    : 0.0;

                // コンポーネント情報の解決: component_guid > type_name > name
                GrasshopperComponentAnalyzer.ComponentInfo? componentInfo = null;

                // 1. component_guid による検索（最優先）
                if (request.Arguments.TryGetValue("component_guid", out var guidValue))
                {
                    var guidStr = guidValue.ToString()?.Trim('"');
                    if (!Guid.TryParse(guidStr, out var guid))
                    {
                        throw new McpProtocolException("Invalid GUID format for component_guid");
                    }
                    componentInfo = _analyzer.GetComponentByGuid(guid);
                    if (componentInfo == null)
                    {
                        throw new McpProtocolException($"Component with GUID '{guid}' not found");
                    }
                }
                // 2. type_name による検索
                else if (request.Arguments.TryGetValue("type_name", out var typeNameValue))
                {
                    var typeName = typeNameValue.ToString()?.Trim('"');
                    componentInfo = _analyzer.GetComponentByTypeName(typeName ?? "");
                    if (componentInfo == null)
                    {
                        throw new McpProtocolException($"Component type '{typeName}' not found");
                    }
                }
                // 3. name による検索（部分一致）
                else if (request.Arguments.TryGetValue("name", out var nameValue))
                {
                    var nameStr = nameValue.ToString()?.Trim('"') ?? "";
                    var matches = _analyzer.SearchByName(nameStr).ToList();
                    if (matches.Count == 0)
                    {
                        throw new McpProtocolException($"No component found matching name '{nameStr}'");
                    }
                    if (matches.Count > 1)
                    {
                        // 完全一致があればそれを優先
                        var exactMatch = matches.FirstOrDefault(m =>
                            m.Name.Equals(nameStr, StringComparison.OrdinalIgnoreCase));
                        if (exactMatch != null)
                        {
                            componentInfo = exactMatch;
                        }
                        else
                        {
                            var names = string.Join(", ", matches.Take(10).Select(m => $"{m.Name} ({m.ComponentGuid})"));
                            throw new McpProtocolException($"Ambiguous name '{nameStr}'. Matches: {names}");
                        }
                    }
                    else
                    {
                        componentInfo = matches[0];
                    }
                }
                else
                {
                    throw new McpProtocolException("One of component_guid, type_name, or name is required");
                }

                // アクティブなGrasshopperドキュメントを取得
                var doc = Instances.ActiveDocument;
                if (doc == null)
                {
                    throw new McpProtocolException("No active Grasshopper document found");
                }

                // コンポーネントのインスタンスを作成
                var component = componentInfo.CreateInstance();

                // コンポーネントをドキュメントに追加
                doc.AddObject(component, false);
                if (component.Attributes != null)
                {
                    component.Attributes.Pivot = new System.Drawing.PointF((float)x, (float)y);
                }

                // UIの更新
                component.ExpireSolution(true);
                RhinoApp.InvokeOnUiThread(() =>
                {
                    Instances.RedrawCanvas();
                });

                // レスポンスを構築
                var response = new
                {
                    status = "success",
                    component = new
                    {
                        guid = component.InstanceGuid.ToString(),
                        component_guid = componentInfo.ComponentGuid.ToString(),
                        name = component.Name,
                        position = new
                        {
                            x,
                            y
                        },
                        parameters = new
                        {
                            input = component is IGH_Component ghComponent
                                ? ghComponent.Params.Input.Select(p => new
                                {
                                    name = p.Name,
                                    nickname = p.NickName,
                                    description = p.Description,
                                    type_name = p.TypeName,
                                    param_id = p.InstanceGuid.ToString(),
                                    optional = p.Optional,
                                }).ToArray()
                                : Array.Empty<object>(),
                            output = component is IGH_Component ghOutputComponent
                                ? ghOutputComponent.Params.Output.Select(p => new
                                {
                                    name = p.Name,
                                    nickname = p.NickName,
                                    description = p.Description,
                                    type_name = p.TypeName,
                                    param_id = p.InstanceGuid.ToString()
                                }).ToArray()
                                : Array.Empty<object>()
                        }
                    }
                };

                return Task.FromResult(new CallToolResult()
                {
                    Content = [new TextContentBlock()
                    {
                        Text = JsonSerializer.Serialize(response, new JsonSerializerOptions
                        {
                            WriteIndented = true
                        }),
                    }]
                });
            }
            catch (McpProtocolException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new McpProtocolException($"Error creating component: {ex.Message}", ex);
            }
        }
    }
}
