using ModelContextProtocol.Protocol.Transport;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Text;
using System.Text.Json;
using System.IO;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;
using Rhino;
using Rhino.Geometry;
using RhinoMCPServer.MCP.Tools;


namespace RhinoMCPServer.MCP
{
    public static class MCPServer
    {
        private static ILoggerFactory CreateLoggerFactory()
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

            return LoggerFactory.Create(builder =>
            {
                builder.AddSerilog();
            });
        }

        public static async Task RunAsync(int port = 3001, CancellationToken cancellationToken = default)
        {
            Console.WriteLine("Starting server...");

            McpServerOptions options = new()
            {
                ServerInfo = new Implementation() { Name = "MCPRhinoServer", Version = "1.0.0" },
                Capabilities = new ServerCapabilities()
                {
                    Tools = new(),
                    Resources = new(),
                    Prompts = new(),
                },
                ProtocolVersion = "2024-11-05",
                ServerInstructions = "This is a Model Context Protocol server for Rhino.",
            };

            IMcpServer? server = null;

            Console.WriteLine("Registering handlers.");

            var toolManager = new ToolManager();

            options.Capabilities = new()
            {
                Tools = new()
                {
                    ListToolsHandler = (request, cancellationToken) => toolManager.ListToolsAsync(),
                    CallToolHandler = async (request, cancellationToken) => await toolManager.ExecuteToolAsync(request.Params, server),
                },
            };

            using var loggerFactory = CreateLoggerFactory();
            var transport = new HttpListenerSseServerTransport("MCPRhinoServer", port, loggerFactory);
            server = McpServerFactory.Create(transport, options, loggerFactory);

            Console.WriteLine("Server initialized.");

            await server.StartAsync(cancellationToken);

            Console.WriteLine("Server started.");

            try
            {
                // Run until process is stopped by the client (parent process) or test
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }
            finally
            {
                await server.DisposeAsync();
            }
        }
    }

}