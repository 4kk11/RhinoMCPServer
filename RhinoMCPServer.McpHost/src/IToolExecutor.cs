using System.Threading.Tasks;

namespace RhinoMCPServer.McpHost;

/// <summary>
/// Interface for executing tools across AssemblyLoadContext boundaries.
/// Uses JSON strings to avoid type boundary issues.
/// </summary>
public interface IToolExecutor
{
    /// <summary>
    /// Gets the list of available tools as a JSON string.
    /// Returns a JSON array of tool definitions.
    /// </summary>
    string ListToolsJson();

    /// <summary>
    /// Executes a tool and returns the result as a JSON string.
    /// </summary>
    /// <param name="toolName">The name of the tool to execute</param>
    /// <param name="argumentsJson">JSON string containing the tool arguments</param>
    /// <returns>JSON string containing the tool result</returns>
    Task<string> ExecuteToolJsonAsync(string toolName, string argumentsJson);
}
