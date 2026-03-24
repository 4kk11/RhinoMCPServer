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
using RhinoMCPServer.Common;
using RhinoMCPTools.Grasshopper.Analysis;

namespace RhinoMCPTools.Grasshopper.Canvas
{
    /// <summary>
    /// ComponentGUIDの逆引き・検証に特化した軽量ツール
    /// </summary>
    public class LookupComponentGuidTool : IMCPTool
    {
        private readonly GrasshopperComponentAnalyzer _analyzer;

        public LookupComponentGuidTool()
        {
            _analyzer = new GrasshopperComponentAnalyzer();
        }

        public string Name => "lookup_component_guid";

        public string Description =>
            "Looks up a Grasshopper component by name or validates a ComponentGUID. " +
            "Returns the correct ComponentGUID and component metadata.";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "name": {
                        "type": "string",
                        "description": "Component name to search for (partial match)"
                    },
                    "component_guid": {
                        "type": "string",
                        "description": "ComponentGUID to validate"
                    },
                    "category": {
                        "type": "string",
                        "description": "Filter by category (optional)"
                    }
                }
            }
            """);

        public Task<CallToolResult> ExecuteAsync(CallToolRequestParams request, McpServer? server)
        {
            try
            {
                string? nameQuery = null;
                string? guidQuery = null;
                string? categoryFilter = null;

                if (request.Arguments != null)
                {
                    if (request.Arguments.TryGetValue("name", out var nameValue))
                        nameQuery = nameValue.ToString()?.Trim('"');
                    if (request.Arguments.TryGetValue("component_guid", out var guidValue))
                        guidQuery = guidValue.ToString()?.Trim('"');
                    if (request.Arguments.TryGetValue("category", out var catValue))
                        categoryFilter = catValue.ToString()?.Trim('"');
                }

                if (string.IsNullOrEmpty(nameQuery) && string.IsNullOrEmpty(guidQuery))
                {
                    throw new McpProtocolException("Either 'name' or 'component_guid' parameter is required");
                }

                object response;

                if (!string.IsNullOrEmpty(guidQuery))
                {
                    // GUID検証モード
                    response = HandleGuidValidation(guidQuery, nameQuery);
                }
                else
                {
                    // Name検索モード
                    response = HandleNameSearch(nameQuery!, categoryFilter);
                }

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
                throw new McpProtocolException($"Error looking up component: {ex.Message}", ex);
            }
        }

        private object HandleGuidValidation(string guidStr, string? nameHint)
        {
            if (!Guid.TryParse(guidStr, out var guid))
            {
                throw new McpProtocolException($"Invalid GUID format: {guidStr}");
            }

            var info = _analyzer.GetComponentByGuid(guid);
            if (info != null)
            {
                return new
                {
                    status = "success",
                    query = new { component_guid = guidStr },
                    is_valid = true,
                    component = new
                    {
                        name = info.Name,
                        component_guid = info.ComponentGuid.ToString(),
                        category = info.Category,
                        sub_category = info.SubCategory,
                        description = info.Description
                    }
                };
            }

            // 見つからない場合、名前ヒントがあれば代替候補を検索
            var suggestions = new List<object>();
            if (!string.IsNullOrEmpty(nameHint))
            {
                suggestions = _analyzer.SearchByName(nameHint)
                    .Take(5)
                    .Select(c => (object)new
                    {
                        name = c.Name,
                        component_guid = c.ComponentGuid.ToString(),
                        category = c.Category,
                        sub_category = c.SubCategory,
                        description = c.Description
                    })
                    .ToList();
            }

            return new
            {
                status = "success",
                query = new { component_guid = guidStr },
                is_valid = false,
                message = $"No component found with GUID {guidStr}",
                suggestions = suggestions.ToArray()
            };
        }

        private object HandleNameSearch(string name, string? categoryFilter)
        {
            var lower = name.ToLowerInvariant();

            var matches = _analyzer.GetAllComponents()
                .Where(c => c.Name.ToLowerInvariant().Contains(lower))
                .Where(c => string.IsNullOrEmpty(categoryFilter) ||
                            c.Category.Equals(categoryFilter, StringComparison.OrdinalIgnoreCase));

            // ソート: 完全一致 > 前方一致 > 部分一致
            var sorted = matches
                .OrderBy(c =>
                {
                    var n = c.Name.ToLowerInvariant();
                    if (n == lower) return 0;           // 完全一致
                    if (n.StartsWith(lower)) return 1;  // 前方一致
                    return 2;                           // 部分一致
                })
                .ThenBy(c => c.Name)
                .Take(10)
                .ToList();

            return new
            {
                status = "success",
                query = new { name },
                results = sorted.Select(c => new
                {
                    name = c.Name,
                    component_guid = c.ComponentGuid.ToString(),
                    category = c.Category,
                    sub_category = c.SubCategory,
                    description = c.Description
                }).ToArray(),
                total_count = sorted.Count
            };
        }
    }
}
