using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using RhinoMCPServer.Common;
using RhinoMCPTools.Grasshopper.Analysis;
using Grasshopper.Kernel;
using Grasshopper;

namespace RhinoMCPTools.Grasshopper.Canvas
{
    /// <summary>
    /// Tool to retrieve a list of available Grasshopper components
    /// </summary>
    public class GetAvailableComponentsTool : IMCPTool
    {
        private readonly GrasshopperComponentAnalyzer _analyzer;

        public string Name => "get_available_components";

        public string Description => "Retrieves a list of available Grasshopper components";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>(@"{
            ""type"": ""object"",
            ""properties"": {
                ""filter"": {
                    ""type"": ""string"",
                    ""description"": ""Filter by component name (optional)"",
                    ""default"": """"
                },
                ""category"": {
                    ""type"": ""string"",
                    ""description"": ""Filter by category (optional)"",
                    ""default"": """"
                }
            }
        }");

        public GetAvailableComponentsTool()
        {
            _analyzer = new GrasshopperComponentAnalyzer();
        }

        public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
        {
            try
            {
                // フィルター条件を取得
                var filter = request.Arguments?.TryGetValue("filter", out var filterValue) == true
                    ? filterValue.ToString().ToLowerInvariant()
                    : "";
                var category = request.Arguments?.TryGetValue("category", out var categoryValue) == true
                    ? categoryValue.ToString()
                    : "";

                // キャッシュからコンポーネント情報を取得し、フィルタリングを適用
                var filteredComponents = _analyzer.GetAllComponents()
                    .Where(c => string.IsNullOrEmpty(filter) || 
                              c.Name.ToLowerInvariant().Contains(filter) || 
                              c.Description.ToLowerInvariant().Contains(filter))
                    .Where(c => string.IsNullOrEmpty(category) || 
                              c.Category.Equals(category, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(c => c.Category)
                    .ThenBy(c => c.SubCategory)
                    .ThenBy(c => c.Name)
                    .ToList();

                // レスポンスを構築
                var response = new
                {
                    components = filteredComponents.Select(c => new
                    {
                        name = c.Name,
                        description = c.Description,
                        type_name = c.FullTypeName,
                        is_param = c.IsParam,
                        category = c.Category,
                        sub_category = c.SubCategory
                    }).ToArray(),
                    total_count = filteredComponents.Count
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
                throw new McpServerException($"Error getting available components: {ex.Message}", ex);
            }
        }
    }
}