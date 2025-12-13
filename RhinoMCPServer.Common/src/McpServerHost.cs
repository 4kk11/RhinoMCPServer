using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace RhinoMCPServer.Common
{
    /// <summary>
    /// MCP Server host that uses AssemblyLoadContext to isolate
    /// the MCP SDK and System.Text.Json 10.x from Rhino8's runtime.
    /// Instance-based design for proper lifecycle management and testability.
    /// </summary>
    public sealed class McpServerHost : IAsyncDisposable
    {
        private readonly ToolManager _toolManager;
        private readonly string _mcpHostPath;
        private readonly string _logDir;

        private McpLoadContext? _loadContext;
        private object? _runner;
        private MethodInfo? _stopMethod;
        private bool _disposed;

        /// <summary>
        /// Creates a new MCP server host.
        /// </summary>
        /// <param name="pluginDirectory">Optional plugin directory for tools. Uses default if null.</param>
        /// <param name="mcpHostPath">Optional path to McpHost assembly. Uses default if null.</param>
        /// <param name="logDir">Optional log directory. Uses default if null.</param>
        public McpServerHost(string? pluginDirectory = null, string? mcpHostPath = null, string? logDir = null)
        {
            _toolManager = new ToolManager(pluginDirectory);

            string assemblyDir = Path.GetDirectoryName(typeof(McpServerHost).Assembly.Location)!;

            if (mcpHostPath == null)
            {
                string mcpHostDir = Path.Combine(assemblyDir, "McpHost");
                _mcpHostPath = Path.Combine(mcpHostDir, "RhinoMCPServer.McpHost.dll");
            }
            else
            {
                _mcpHostPath = mcpHostPath;
            }

            _logDir = logDir ?? Path.Combine(assemblyDir, "logs");
        }

        /// <summary>
        /// Gets whether the server is currently running.
        /// </summary>
        public bool IsRunning => _runner != null;

        /// <summary>
        /// Starts the MCP server on the specified port.
        /// </summary>
        /// <param name="port">HTTP port to listen on</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public async Task RunAsync(int port = 3001, CancellationToken cancellationToken = default)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(McpServerHost));
            }

            if (_runner != null)
            {
                throw new InvalidOperationException("Server is already running.");
            }

            // Step 1: Initialize ToolManager (deferred initialization)
            _toolManager.Initialize();
            var toolExecutor = new ToolExecutorAdapter(_toolManager);

            // Step 2: Verify McpHost assembly exists
            if (!File.Exists(_mcpHostPath))
            {
                throw new FileNotFoundException(
                    $"McpHost assembly not found at: {_mcpHostPath}\n" +
                    "Make sure the McpHost project is built and files are copied to the McpHost directory.");
            }

            // Step 3: Create isolated load context
            _loadContext = new McpLoadContext(_mcpHostPath);

            // Step 4: Load the McpHost assembly in isolated context
            var mcpHostAssembly = _loadContext.LoadFromAssemblyPath(_mcpHostPath);

            // Step 5: Create a wrapper that implements IToolExecutor in the isolated context
            var toolExecutorWrapper = CreateToolExecutorProxy(mcpHostAssembly, toolExecutor);

            // Step 6: Create McpHostRunner instance via reflection
            var runnerType = mcpHostAssembly.GetType("RhinoMCPServer.McpHost.McpHostRunner")!;
            _runner = Activator.CreateInstance(runnerType, toolExecutorWrapper);
            _stopMethod = runnerType.GetMethod("StopAsync");

            // Step 7: Call RunAsync on the runner
            var runMethod = runnerType.GetMethod("RunAsync")!;
            var runTask = (Task)runMethod.Invoke(_runner, new object[] { port, _logDir, cancellationToken })!;

            await runTask;
        }

        /// <summary>
        /// Creates a proxy object that implements IToolExecutor from the McpHost context
        /// but delegates to our ToolExecutorAdapter.
        /// </summary>
        private static object CreateToolExecutorProxy(Assembly mcpHostAssembly, ToolExecutorAdapter adapter)
        {
            // Try ToolExecutorProxy first
            var proxyType = mcpHostAssembly.GetType("RhinoMCPServer.McpHost.ToolExecutorProxy");

            if (proxyType != null)
            {
                return Activator.CreateInstance(proxyType, adapter)!;
            }

            // Fall back to DelegateToolExecutor
            var delegateType = mcpHostAssembly.GetType("RhinoMCPServer.McpHost.DelegateToolExecutor");

            if (delegateType != null)
            {
                Func<string> listToolsFunc = adapter.ListToolsJson;
                Func<string, string, Task<string>> executeToolFunc = adapter.ExecuteToolJsonAsync;

                return Activator.CreateInstance(delegateType, listToolsFunc, executeToolFunc)!;
            }

            throw new InvalidOperationException(
                "Neither ToolExecutorProxy nor DelegateToolExecutor found in McpHost assembly. " +
                "Make sure the McpHost project includes one of these types.");
        }

        /// <summary>
        /// Stops the MCP server.
        /// </summary>
        public async Task StopAsync()
        {
            if (_runner != null && _stopMethod != null)
            {
                var stopTask = (Task?)_stopMethod.Invoke(_runner, Array.Empty<object>());
                if (stopTask != null)
                {
                    await stopTask;
                }
            }

            _runner = null;
            _stopMethod = null;

            if (_loadContext != null)
            {
                _loadContext.Unload();
                _loadContext = null;
            }
        }

        /// <summary>
        /// Disposes the MCP server host and all resources.
        /// </summary>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            await StopAsync();
            _toolManager.Dispose();
            _disposed = true;
        }
    }
}
