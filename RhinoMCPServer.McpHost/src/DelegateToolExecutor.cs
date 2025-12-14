using System;
using System.Threading.Tasks;

namespace RhinoMCPServer.McpHost;

/// <summary>
/// IToolExecutor implementation that delegates to functions passed from the default context.
/// This allows communication across AssemblyLoadContext boundaries using delegates.
/// </summary>
public class DelegateToolExecutor : IToolExecutor
{
    private readonly Func<string> _listToolsFunc;
    private readonly Func<string, string, Task<string>> _executeToolFunc;

    /// <summary>
    /// Creates a new DelegateToolExecutor with the specified delegate functions.
    /// </summary>
    /// <param name="listToolsFunc">Function that returns JSON array of tool definitions</param>
    /// <param name="executeToolFunc">Function that executes a tool and returns JSON result</param>
    public DelegateToolExecutor(
        Func<string> listToolsFunc,
        Func<string, string, Task<string>> executeToolFunc)
    {
        _listToolsFunc = listToolsFunc ?? throw new ArgumentNullException(nameof(listToolsFunc));
        _executeToolFunc = executeToolFunc ?? throw new ArgumentNullException(nameof(executeToolFunc));
    }

    /// <inheritdoc />
    public string ListToolsJson()
    {
        return _listToolsFunc();
    }

    /// <inheritdoc />
    public Task<string> ExecuteToolJsonAsync(string toolName, string argumentsJson)
    {
        return _executeToolFunc(toolName, argumentsJson);
    }
}
