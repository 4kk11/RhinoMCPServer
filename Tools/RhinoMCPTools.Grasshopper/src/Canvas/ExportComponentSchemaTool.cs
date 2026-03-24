using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Grasshopper;
using Grasshopper.Kernel;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Rhino;
using RhinoMCPServer.Common;
using RhinoMCPTools.Grasshopper.Analysis;

namespace RhinoMCPTools.Grasshopper.Canvas
{
    /// <summary>
    /// コンポーネントのポート構成をGrassDSLスキーマ互換JSONで出力するツール。
    /// インメモリでインスタンス化するためキャンバスへの配置は不要。
    /// </summary>
    public class ExportComponentSchemaTool : IMCPTool
    {
        private readonly GrasshopperComponentAnalyzer _analyzer;

        // IGH_Param.TypeName → GrassDSL type
        private static readonly Dictionary<string, string> TypeMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Point"] = "point",
            ["Number"] = "number",
            ["Integer"] = "integer",
            ["Boolean"] = "boolean",
            ["Curve"] = "curve",
            ["Line"] = "line",
            ["Surface"] = "surface",
            ["Brep"] = "brep",
            ["Vector"] = "vector",
            ["Plane"] = "plane",
            ["Geometry"] = "geometry",
            ["Text"] = "string",
            ["Colour"] = "color",
            ["Transform"] = "transform",
            ["Mesh"] = "mesh",
            ["Arc"] = "arc",
            ["Circle"] = "circle",
            ["Rectangle"] = "rectangle",
            ["Box"] = "box",
            ["SubD"] = "subd",
        };

        public ExportComponentSchemaTool()
        {
            _analyzer = new GrasshopperComponentAnalyzer();
        }

        public string Name => "export_component_schema";

        public string Description =>
            "Exports Grasshopper component port definitions as GrassDSL-compatible schema JSON. " +
            "Creates instances in-memory without modifying the canvas. " +
            "Use this to auto-generate schemas/core.json entries for GrassDSL.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "component_guids": {
                        "type": "array",
                        "items": { "type": "string" },
                        "description": "List of ComponentGUIDs to export"
                    },
                    "names": {
                        "type": "array",
                        "items": { "type": "string" },
                        "description": "List of component names to export (exact match preferred, falls back to partial)"
                    },
                    "category": {
                        "type": "string",
                        "description": "Export all components in this category (e.g. 'Curve', 'Surface')"
                    }
                }
            }
            """);

        public Task<CallToolResult> ExecuteAsync(CallToolRequestParams request, McpServer? server)
        {
            try
            {
                var targetInfos = ResolveTargets(request);

                if (targetInfos.Count == 0)
                {
                    throw new McpProtocolException(
                        "No components specified. Provide component_guids, names, or category.");
                }

                // UIスレッドでインスタンス生成してポート情報を取得
                object? result = null;
                RhinoApp.InvokeOnUiThread(new Action(() =>
                {
                    var schemas = new Dictionary<string, object>();
                    var errors = new List<string>();

                    foreach (var info in targetInfos)
                    {
                        try
                        {
                            var schema = BuildSchemaEntry(info);
                            if (schema != null)
                            {
                                // スキーマ名: スペース除去して PascalCase
                                var schemaName = info.Name.Replace(" ", "");
                                // 重複回避
                                if (schemas.ContainsKey(schemaName))
                                {
                                    schemaName = $"{schemaName}_{info.ComponentGuid.ToString()[..8]}";
                                }
                                schemas[schemaName] = schema;
                            }
                        }
                        catch (Exception ex)
                        {
                            errors.Add($"{info.Name} ({info.ComponentGuid}): {ex.Message}");
                        }
                    }

                    result = new
                    {
                        status = "success",
                        components = schemas,
                        exported_count = schemas.Count,
                        errors = errors.Count > 0 ? errors.ToArray() : Array.Empty<string>()
                    };
                }));

                if (result == null)
                {
                    throw new McpProtocolException("Unexpected error: no result from UI thread operation");
                }

                return Task.FromResult(new CallToolResult()
                {
                    Content = [new TextContentBlock()
                    {
                        Text = JsonSerializer.Serialize(result, new JsonSerializerOptions
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
                throw new McpProtocolException($"Error exporting component schema: {ex.Message}", ex);
            }
        }

        private List<GrasshopperComponentAnalyzer.ComponentInfo> ResolveTargets(CallToolRequestParams request)
        {
            var targets = new List<GrasshopperComponentAnalyzer.ComponentInfo>();
            var seen = new HashSet<Guid>();

            if (request.Arguments == null) return targets;

            // 1. component_guids
            if (request.Arguments.TryGetValue("component_guids", out var guidsValue) &&
                guidsValue is JsonElement guidsArray && guidsArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in guidsArray.EnumerateArray())
                {
                    var guidStr = element.GetString()?.Trim('"');
                    if (Guid.TryParse(guidStr, out var guid))
                    {
                        var info = _analyzer.GetComponentByGuid(guid);
                        if (info != null && seen.Add(info.ComponentGuid))
                        {
                            targets.Add(info);
                        }
                    }
                }
            }

            // 2. names
            if (request.Arguments.TryGetValue("names", out var namesValue) &&
                namesValue is JsonElement namesArray && namesArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in namesArray.EnumerateArray())
                {
                    var name = element.GetString()?.Trim('"');
                    if (string.IsNullOrEmpty(name)) continue;

                    // 完全一致を優先
                    var matches = _analyzer.SearchByName(name).ToList();
                    var exact = matches.FirstOrDefault(m =>
                        m.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                    var match = exact ?? matches.FirstOrDefault();
                    if (match != null && seen.Add(match.ComponentGuid))
                    {
                        targets.Add(match);
                    }
                }
            }

            // 3. category
            if (request.Arguments.TryGetValue("category", out var catValue))
            {
                var category = catValue.ToString()?.Trim('"');
                if (!string.IsNullOrEmpty(category))
                {
                    var categoryMatches = _analyzer.GetAllComponents()
                        .Where(c => c.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                        .Where(c => !c.IsParam) // パラメータ型は除外
                        .Where(c => seen.Add(c.ComponentGuid));
                    targets.AddRange(categoryMatches);
                }
            }

            return targets;
        }

        private object? BuildSchemaEntry(GrasshopperComponentAnalyzer.ComponentInfo info)
        {
            var instance = info.CreateInstance();
            if (instance is not IGH_Component comp)
            {
                return null; // パラメータ型など、IGH_Component でないものはスキップ
            }

            // 入力ポート
            var inputs = new Dictionary<string, object>();
            var inputOrder = new List<string>();

            foreach (var p in comp.Params.Input)
            {
                var key = string.IsNullOrEmpty(p.NickName) ? p.Name : p.NickName;
                var dslType = MapType(p.TypeName);

                var portDef = new Dictionary<string, object>
                {
                    ["type"] = dslType,
                    ["variadic"] = false,
                };

                if (p.Optional)
                {
                    portDef["optional"] = true;
                }

                inputs[key] = portDef;
                inputOrder.Add(key);
            }

            // 出力ポート
            var outputs = new Dictionary<string, object>();
            var outputOrder = new List<string>();

            foreach (var p in comp.Params.Output)
            {
                var key = string.IsNullOrEmpty(p.NickName) ? p.Name : p.NickName;
                var dslType = MapType(p.TypeName);

                outputs[key] = new Dictionary<string, object>
                {
                    ["type"] = dslType,
                    ["variadic"] = false,
                };
                outputOrder.Add(key);
            }

            return new
            {
                guid = info.ComponentGuid.ToString(),
                inputs,
                input_order = inputOrder.ToArray(),
                outputs,
                output_order = outputOrder.ToArray()
            };
        }

        private static string MapType(string? typeName)
        {
            if (string.IsNullOrEmpty(typeName)) return "generic";

            // 完全一致
            if (TypeMap.TryGetValue(typeName, out var mapped))
            {
                return mapped;
            }

            // 部分一致（"3D Point" → "point" 等）
            foreach (var kvp in TypeMap)
            {
                if (typeName.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return "generic";
        }
    }
}
