using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using ModelContextProtocol.Protocol.Types;
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
    /// </summary>
    public class CreateComponentTool : IMCPTool
    {
        private readonly GrasshopperComponentAnalyzer _analyzer;

        public CreateComponentTool()
        {
            _analyzer = new GrasshopperComponentAnalyzer();
        }

        public string Name => "create_component";

        public string Description => "指定されたGrasshopperコンポーネントを作成します";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>(@"{
            ""type"": ""object"",
            ""properties"": {
                ""type_name"": {
                    ""type"": ""string"",
                    ""description"": ""作成するコンポーネントの完全修飾名""
                },
                ""x"": {
                    ""type"": ""number"",
                    ""description"": ""キャンバス上のX座標"",
                    ""default"": 0
                },
                ""y"": {
                    ""type"": ""number"",
                    ""description"": ""キャンバス上のY座標"",
                    ""default"": 0
                }
            },
            ""required"": [""type_name""]
        }");

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            try
            {
                // パラメータの取得
                if (!request.Arguments.TryGetValue("type_name", out var typeNameValue))
                {
                    throw new McpServerException("type_name parameter is required");
                }
                var typeName = typeNameValue.ToString();

                var x = request.Arguments.TryGetValue("x", out var xValue)
                    ? Convert.ToDouble(xValue.ToString())
                    : 0.0;
                var y = request.Arguments.TryGetValue("y", out var yValue)
                    ? Convert.ToDouble(yValue.ToString())
                    : 0.0;

                // アクティブなGrasshopperドキュメントを取得
                var doc = Instances.ActiveDocument;
                if (doc == null)
                {
                    throw new McpServerException("No active Grasshopper document found");
                }

                // キャッシュからコンポーネント情報を取得
                var componentInfo = _analyzer.GetComponentByTypeName(typeName);
                if (componentInfo == null)
                {
                    throw new McpServerException($"Component type '{typeName}' not found");
                }

                // コンポーネントの型を取得
                var componentType = Type.GetType(typeName);
                if (componentType == null)
                {
                    // Type.GetTypeで見つからない場合は、AppDomainから検索
                    componentType = AppDomain.CurrentDomain.GetAssemblies()
                        .Where(a => !a.IsDynamic)
                        .SelectMany(a =>
                        {
                            try
                            {
                                return a.GetTypes();
                            }
                            catch
                            {
                                return Type.EmptyTypes;
                            }
                        })
                        .FirstOrDefault(t => t.FullName == typeName);

                    if (componentType == null)
                    {
                        throw new McpServerException($"Component type '{typeName}' not found");
                    }
                }

                // コンポーネントのインスタンスを作成
                if (Activator.CreateInstance(componentType) is not IGH_DocumentObject component)
                {
                    throw new McpServerException($"Failed to create component of type '{typeName}'");
                }

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
                                    param_id = p.InstanceGuid.ToString()
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
                throw new McpServerException($"Error creating component: {ex.Message}", ex);
            }
        }
    }
}