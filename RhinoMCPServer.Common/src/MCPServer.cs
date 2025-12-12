using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Serilog;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace RhinoMCPServer.Common
{
    public static class MCPServer
    {
        private static WebApplication? _app;
        private static ToolManager? _toolManager;
        private static CancellationTokenSource? _cts;

        private static void ConfigureLogging()
        {
            // Use serilog
            string pluginPath = Path.GetDirectoryName(typeof(MCPServer).Assembly.Location)!;
            string logDir = Path.Combine(pluginPath, "logs");
            Directory.CreateDirectory(logDir); // logsディレクトリが存在しない場合は作成

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose() // Capture all log levels
                .WriteTo.File(Path.Combine(logDir, "MCPRhinoServer_.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }

        public static async Task RunAsync(int port = 3001, CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Starting server...");

            ConfigureLogging();

            _toolManager = new ToolManager();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            var builder = WebApplication.CreateBuilder();

            // Configure Serilog
            builder.Logging.ClearProviders();
            builder.Logging.AddSerilog();

            // Configure MCP Server with HTTP transport
            builder.Services.AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation() { Name = "MCPRhinoServer", Version = "1.0.0" };
                options.Capabilities = new ServerCapabilities()
                {
                    Tools = new ToolsCapability(),
                    Resources = new ResourcesCapability(),
                    Prompts = new PromptsCapability(),
                };
                options.ProtocolVersion = "2024-11-05";
                options.ServerInstructions = "This is a Model Context Protocol server for Rhino.";
            })
            .WithHttpTransport()
            .WithListToolsHandler(ListToolsHandler)
            .WithCallToolHandler(CallToolHandler);

            Console.WriteLine("Server initialized.");

            _app = builder.Build();
            _app.MapMcp();

            Console.WriteLine($"Server starting on http://localhost:{port}");

            try
            {
                await _app.RunAsync($"http://localhost:{port}");
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Server stopped.");
            }
        }

        public static async Task StopAsync()
        {
            if (_app != null)
            {
                await _app.StopAsync();
                await _app.DisposeAsync();
                _app = null;
            }

            if (_toolManager != null)
            {
                _toolManager.Dispose();
                _toolManager = null;
            }

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }

        private static ValueTask<ListToolsResult> ListToolsHandler(
            RequestContext<ListToolsRequestParams> context,
            CancellationToken cancellationToken)
        {
            return _toolManager!.ListToolsAsync();
        }

        private static async ValueTask<CallToolResult> CallToolHandler(
            RequestContext<CallToolRequestParams> context,
            CancellationToken cancellationToken)
        {
            return await _toolManager!.ExecuteToolAsync(context.Params!, context.Server);
        }
    }
}
