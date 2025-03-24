using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace RhinoMCPServer.Common
{
    public class ToolManager
    {
        private readonly ToolPluginLoader _pluginLoader;
        private readonly string _pluginDirectory;

        public ToolManager()
        {
            // プラグインディレクトリを設定（RhinoのMacPluginsディレクトリ内のToolsフォルダ）
            string pluginPath = Path.GetDirectoryName(typeof(ToolManager).Assembly.Location)!;
            _pluginDirectory = Path.Combine(pluginPath, "Tools");
            
            // プラグインディレクトリが存在しない場合は作成
            Directory.CreateDirectory(_pluginDirectory);

            _pluginLoader = new ToolPluginLoader(_pluginDirectory);
            
            // プラグインを読み込む
            _pluginLoader.LoadPlugins();
        }

        public Task<ListToolsResult> ListToolsAsync()
        {
            var tools = _pluginLoader.LoadedTools.Values.Select(t => new Tool
            {
                Name = t.Name,
                Description = t.Description,
                InputSchema = t.InputSchema
            }).ToList();

            return Task.FromResult(new ListToolsResult { Tools = tools });
        }

        public Task<CallToolResponse> ExecuteToolAsync(CallToolRequestParams request, IMcpServer? server)
        {
            if (request.Name is null)
            {
                throw new McpServerException("Missing required parameter 'name'");
            }

            if (!_pluginLoader.LoadedTools.TryGetValue(request.Name, out var tool))
            {
                throw new McpServerException($"Unknown tool: {request.Name}");
            }

            return tool.ExecuteAsync(request, server);
        }

        public void Dispose()
        {
            _pluginLoader.UnloadPlugins();
        }
    }
}