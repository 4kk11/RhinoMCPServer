using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using System.Text.Json;
using System.Threading.Tasks;

namespace RhinoMCPServer.Tools
{
    public interface IMCPTool
    {
        string Name { get; }
        string Description { get; }
        JsonElement InputSchema { get; }
        Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server);
    }
}