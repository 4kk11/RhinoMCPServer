using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace RhinoMCPServer.Common
{
    /// <summary>
    /// Manages MCP tool plugins.
    /// Follows explicit initialization pattern - call Initialize() after construction.
    /// </summary>
    public sealed class ToolManager : IDisposable
    {
        private readonly ToolPluginLoader _pluginLoader;
        private readonly string _pluginDirectory;
        private bool _initialized;
        private bool _disposed;

        /// <summary>
        /// Creates a new ToolManager with the specified plugin directory.
        /// Note: Does not automatically load plugins. Call Initialize() explicitly.
        /// </summary>
        /// <param name="pluginDirectory">Directory containing tool plugins. If null, uses default Tools directory.</param>
        public ToolManager(string? pluginDirectory = null)
        {
            if (pluginDirectory == null)
            {
                string pluginPath = Path.GetDirectoryName(typeof(ToolManager).Assembly.Location)!;
                _pluginDirectory = Path.Combine(pluginPath, "Tools");
            }
            else
            {
                _pluginDirectory = pluginDirectory;
            }

            _pluginLoader = new ToolPluginLoader(_pluginDirectory);
        }

        /// <summary>
        /// Initializes the ToolManager by creating the plugin directory and loading plugins.
        /// This method is idempotent - calling it multiple times has no additional effect.
        /// </summary>
        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            // プラグインディレクトリが存在しない場合は作成
            Directory.CreateDirectory(_pluginDirectory);

            // プラグインを読み込む
            _pluginLoader.LoadPlugins();
            _initialized = true;
        }

        /// <summary>
        /// Gets the loaded tools collection.
        /// </summary>
        public IReadOnlyDictionary<string, IMCPTool> LoadedTools => _pluginLoader.LoadedTools;

        public Task<CallToolResult> ExecuteToolAsync(CallToolRequestParams request, McpServer? server)
        {
            if (request.Name is null)
            {
                throw new McpProtocolException("Missing required parameter 'name'", McpErrorCode.InvalidParams);
            }

            if (!_pluginLoader.LoadedTools.TryGetValue(request.Name, out var tool))
            {
                throw new McpProtocolException($"Unknown tool: {request.Name}", McpErrorCode.InvalidRequest);
            }

            return tool.ExecuteAsync(request, server);
        }

        /// <summary>
        /// Releases all resources used by the ToolManager.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _pluginLoader.UnloadPlugins();
            _disposed = true;
        }
    }
}