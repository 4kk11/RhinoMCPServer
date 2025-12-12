using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace RhinoMCPServer.Common
{
    /// <summary>
    /// MCP Server entry point that uses AssemblyLoadContext to isolate
    /// the MCP SDK and System.Text.Json 10.x from Rhino8's runtime.
    /// </summary>
    public static class MCPServer
    {
        private static McpLoadContext? _loadContext;
        private static object? _runner;
        private static MethodInfo? _stopMethod;
        private static ToolManager? _toolManager;

        /// <summary>
        /// Starts the MCP server on the specified port.
        /// </summary>
        /// <param name="port">HTTP port to listen on</param>
        /// <param name="cancellationToken">Cancellation token</param>
        public static async Task RunAsync(int port = 3001, CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Starting MCP server with isolated AssemblyLoadContext...");

            // Step 1: Create ToolManager (in default context)
            _toolManager = new ToolManager();
            var toolExecutor = new ToolExecutorAdapter(_toolManager);

            // Step 2: Find the McpHost assembly path
            string assemblyDir = Path.GetDirectoryName(typeof(MCPServer).Assembly.Location)!;
            string mcpHostDir = Path.Combine(assemblyDir, "McpHost");
            string mcpHostPath = Path.Combine(mcpHostDir, "RhinoMCPServer.McpHost.dll");

            Console.WriteLine($"McpHost assembly path: {mcpHostPath}");

            if (!File.Exists(mcpHostPath))
            {
                throw new FileNotFoundException(
                    $"McpHost assembly not found at: {mcpHostPath}\n" +
                    "Make sure the McpHost project is built and files are copied to the McpHost directory.");
            }

            Console.WriteLine($"Loading McpHost from: {mcpHostPath}");

            // Step 3: Create isolated load context
            _loadContext = new McpLoadContext(mcpHostPath);

            // Step 4: Load the McpHost assembly in isolated context
            var mcpHostAssembly = _loadContext.LoadFromAssemblyPath(mcpHostPath);

            // Step 5: Create a wrapper that implements IToolExecutor in the isolated context
            var toolExecutorWrapper = CreateToolExecutorProxyType(mcpHostAssembly, toolExecutor);

            // Step 6: Create McpHostRunner instance via reflection
            var runnerType = mcpHostAssembly.GetType("RhinoMCPServer.McpHost.McpHostRunner")!;
            _runner = Activator.CreateInstance(runnerType, toolExecutorWrapper);
            _stopMethod = runnerType.GetMethod("StopAsync");

            // Step 7: Get log directory
            string logDir = Path.Combine(assemblyDir, "logs");

            // Step 8: Call RunAsync on the runner
            var runMethod = runnerType.GetMethod("RunAsync")!;
            var runTask = (Task)runMethod.Invoke(_runner, new object[] { port, logDir, cancellationToken })!;

            await runTask;
        }

        /// <summary>
        /// Creates a proxy object that implements IToolExecutor from the McpHost context
        /// but delegates to our ToolExecutorAdapter.
        /// </summary>
        private static object CreateToolExecutorProxyType(Assembly mcpHostAssembly, ToolExecutorAdapter adapter)
        {
            // Since IToolExecutor is defined in McpHost (isolated context),
            // we need to create a proxy class dynamically or use a different approach.
            //
            // The simplest approach is to create a wrapper class in McpHost that
            // accepts delegate functions. But since we can't easily pass delegates
            // across context boundaries, we'll use a different strategy:
            //
            // We'll create a ToolExecutorProxy class that wraps the adapter and
            // is designed to work across the boundary.

            // For now, create a simple wrapper using reflection
            var proxyType = mcpHostAssembly.GetType("RhinoMCPServer.McpHost.ToolExecutorProxy");

            if (proxyType != null)
            {
                // If ToolExecutorProxy exists, use it
                return Activator.CreateInstance(proxyType, adapter)!;
            }

            // If proxy doesn't exist, we need to use a callback-based approach
            // Create a DelegateToolExecutor that wraps our adapter
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
        public static async Task StopAsync()
        {
            if (_runner != null && _stopMethod != null)
            {
                var stopTask = (Task?)_stopMethod.Invoke(_runner, Array.Empty<object>());
                if (stopTask != null)
                {
                    await stopTask;
                }
            }

            _toolManager?.Dispose();
            _toolManager = null;

            if (_loadContext != null)
            {
                _loadContext.Unload();
                _loadContext = null;
            }

            _runner = null;
            _stopMethod = null;

            Console.WriteLine("MCP server stopped.");
        }
    }
}
