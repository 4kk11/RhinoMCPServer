using System;
using ModelContextProtocol.Protocol.Types;
using ModelContextProtocol.Server;
using System.Text.Json;
using System.Threading.Tasks;

namespace RhinoMCPServer.Common
{
    /// <summary>
    /// MCPツールプラグインのインターフェース
    /// </summary>
    public interface IMCPTool
    {
        /// <summary>
        /// ツールの名前
        /// </summary>
        string Name { get; }

        /// <summary>
        /// ツールの説明
        /// </summary>
        string Description { get; }

        /// <summary>
        /// ツールの入力スキーマ
        /// </summary>
        JsonElement InputSchema { get; }

        /// <summary>
        /// ツールの実行
        /// </summary>
        Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server);
    }

    /// <summary>
    /// MCPツールプラグインを識別するための属性
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class MCPToolPluginAttribute : Attribute
    {
        /// <summary>
        /// プラグインの名前
        /// </summary>
        public string Name { get; }

        public MCPToolPluginAttribute(string name)
        {
            Name = name;
        }
    }
}