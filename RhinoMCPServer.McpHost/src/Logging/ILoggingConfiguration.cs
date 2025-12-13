using Microsoft.Extensions.Logging;

namespace RhinoMCPServer.McpHost.Logging;

/// <summary>
/// Interface for logging configuration.
/// Follows Martin's Dependency Inversion principle.
/// </summary>
public interface ILoggingConfiguration
{
    /// <summary>
    /// Configures logging for the specified log directory.
    /// </summary>
    /// <param name="logDir">The directory for log files</param>
    /// <returns>The configured logger factory</returns>
    ILoggerFactory Configure(string logDir);
}
