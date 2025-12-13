using System;
using System.Threading;
using System.Threading.Tasks;
using Rhino;
using Rhino.Commands;
using Rhino.Input;
using Rhino.Input.Custom;
using RhinoMCPServer.Common;

namespace RhinoMCPServer.Plugin.Commands
{
    /// <summary>
    /// Rhino command for starting and stopping the MCP server.
    /// </summary>
    public class MCPServerCommand : Command
    {
        private static McpServerHost? _serverHost;
        private static CancellationTokenSource? _cancellationTokenSource;
        private static Task? _serverTask;

        public MCPServerCommand()
        {
            Instance = this;
        }

        public static MCPServerCommand? Instance { get; private set; }

        public override string EnglishName => "MCPServer";

        protected override Result RunCommand(RhinoDoc doc, RunMode mode)
        {
            try
            {
                // サーバーが既に起動している場合は停止
                if (_serverHost != null && _serverHost.IsRunning)
                {
                    string answer = "";
                    var result = RhinoGet.GetString("Server is running. Do you want to stop it? (Yes/No)", false, ref answer);
                    if (result == Result.Success && answer.ToLower().StartsWith("y"))
                    {
                        StopServer();
                        RhinoApp.WriteLine("Server stopped.");
                        return Result.Success;
                    }
                    return Result.Cancel;
                }

                // ポート番号の入力（オプション）
                var gs = new GetString();
                gs.SetCommandPrompt("Enter port number (press Enter for default 3001)");
                gs.AcceptNothing(true);
                gs.Get();

                if (gs.CommandResult() != Result.Success)
                    return gs.CommandResult();

                int port = 3001; // デフォルトポート
                if (!string.IsNullOrEmpty(gs.StringResult()))
                {
                    if (!int.TryParse(gs.StringResult(), out port))
                    {
                        RhinoApp.WriteLine("Invalid port number. Using default port 3001.");
                        port = 3001;
                    }
                }

                StartServer(port);
                RhinoApp.WriteLine($"MCP Server started on port {port}");
                return Result.Success;
            }
            catch (Exception ex)
            {
                RhinoApp.WriteLine($"Error starting server: {ex.Message}");
                return Result.Failure;
            }
        }

        private void StartServer(int port = 3001)
        {
            _serverHost = new McpServerHost();
            _cancellationTokenSource = new CancellationTokenSource();
            _serverTask = _serverHost.RunAsync(port, _cancellationTokenSource.Token);
        }

        private void StopServer()
        {
            if (_cancellationTokenSource != null)
            {
                _cancellationTokenSource.Cancel();
                _cancellationTokenSource.Dispose();
                _cancellationTokenSource = null;
            }

            if (_serverTask != null)
            {
                try
                {
                    _serverTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch (Exception ex)
                {
                    RhinoApp.WriteLine($"Error stopping server: {ex.Message}");
                }
                _serverTask = null;
            }

            if (_serverHost != null)
            {
                _serverHost.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(5));
                _serverHost = null;
            }
        }
    }
}
