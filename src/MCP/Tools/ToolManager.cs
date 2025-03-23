using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RhinoMCPServer.MCP.Tools
{
    public class ToolManager
    {
        private readonly Dictionary<string, IMCPTool> _tools;

        public ToolManager()
        {
            var tools = new List<IMCPTool>
            {
                new EchoTool(),
                new SampleLLMTool(),
                new SphereTool(),
                new DeleteObjectTool(),
                new PolylineTool()
            };

            _tools = tools.ToDictionary(t => t.Name);
        }

        public Task<ListToolsResult> ListToolsAsync()
        {
            var tools = _tools.Values.Select(t => new Tool
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = t.InputSchema
            }).ToList();

            return Task.FromResult(new ListToolsResult { Tools = tools });
        }

        public async Task<CallToolResponse> ExecuteToolAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Name is null)
            {
                throw new McpServerException("Missing required parameter 'name'");
            }

            if (!_tools.TryGetValue(request.Name, out var tool))
            {
                throw new McpServerException($"Unknown tool: {request.Name}");
            }

            return await tool.ExecuteAsync(request, server);
        }
    }
}