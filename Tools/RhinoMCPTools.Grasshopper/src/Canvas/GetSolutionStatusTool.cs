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

namespace RhinoMCPTools.Grasshopper.Canvas
{
    /// <summary>
    /// アクティブドキュメントのソリューション実行状態と、全コンポーネントのエラー・警告を一括取得する
    /// </summary>
    public class GetSolutionStatusTool : IMCPTool
    {
        public string Name => "get_solution_status";

        public string Description => "Retrieves the solution status and all runtime messages from every component in the active Grasshopper document";

        public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>("""
            {
                "type": "object",
                "properties": {
                    "include_messages": {
                        "type": "boolean",
                        "description": "Whether to include detailed messages for each component",
                        "default": true
                    }
                }
            }
            """);

        public Task<CallToolResult> ExecuteAsync(CallToolRequestParams request, McpServer? server)
        {
            try
            {
                var doc = Instances.ActiveCanvas?.Document;
                if (doc == null)
                {
                    throw new McpProtocolException("No active Grasshopper document found");
                }

                bool includeMessages = true;
                if (request.Arguments != null &&
                    request.Arguments.TryGetValue("include_messages", out var includeValue) &&
                    includeValue is JsonElement includeElement)
                {
                    includeMessages = includeElement.GetBoolean();
                }

                int componentCount = 0;
                int errorCount = 0;
                int warningCount = 0;
                int remarkCount = 0;
                var componentsWithErrors = new List<object>();
                var componentsWithWarnings = new List<object>();

                foreach (var obj in doc.Objects)
                {
                    if (obj is not IGH_ActiveObject active)
                    {
                        continue;
                    }

                    componentCount++;

                    var errors = active.RuntimeMessages(GH_RuntimeMessageLevel.Error);
                    var warnings = active.RuntimeMessages(GH_RuntimeMessageLevel.Warning);
                    var remarks = active.RuntimeMessages(GH_RuntimeMessageLevel.Remark);

                    errorCount += errors.Count;
                    warningCount += warnings.Count;
                    remarkCount += remarks.Count;

                    if (errors.Count > 0)
                    {
                        componentsWithErrors.Add(new
                        {
                            guid = obj.InstanceGuid.ToString(),
                            name = obj.Name,
                            nickname = obj.NickName,
                            errors = includeMessages
                                ? errors.Cast<string>().ToArray()
                                : Array.Empty<string>(),
                            warnings = includeMessages
                                ? warnings.Cast<string>().ToArray()
                                : Array.Empty<string>()
                        });
                    }
                    else if (warnings.Count > 0)
                    {
                        componentsWithWarnings.Add(new
                        {
                            guid = obj.InstanceGuid.ToString(),
                            name = obj.Name,
                            nickname = obj.NickName,
                            errors = Array.Empty<string>(),
                            warnings = includeMessages
                                ? warnings.Cast<string>().ToArray()
                                : Array.Empty<string>()
                        });
                    }
                }

                var response = new
                {
                    status = "success",
                    solution = new
                    {
                        component_count = componentCount,
                        error_count = errorCount,
                        warning_count = warningCount,
                        remark_count = remarkCount,
                        components_with_errors = componentsWithErrors.ToArray(),
                        components_with_warnings = componentsWithWarnings.ToArray()
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
                throw new McpProtocolException($"Error getting solution status: {ex.Message}", ex);
            }
        }
    }
}
