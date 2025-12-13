using Microsoft.Extensions.Logging;
using Serilog;

namespace RhinoMCPServer.McpHost.Logging;

/// <summary>
/// Default logging configuration using Serilog.
/// </summary>
public class LoggingConfiguration : ILoggingConfiguration
{
    /// <inheritdoc />
    public ILoggerFactory Configure(string logDir)
    {
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.File(Path.Combine(logDir, "MCPRhinoServer_.log"),
                rollingInterval: RollingInterval.Day,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        return LoggerFactory.Create(builder =>
        {
            builder.AddSerilog();
        });
    }
}
