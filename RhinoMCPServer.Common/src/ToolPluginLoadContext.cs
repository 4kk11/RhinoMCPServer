using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace RhinoMCPServer.Common
{
    /// <summary>
    /// MCPツールプラグイン用のAssemblyLoadContext。
    /// プラグインの依存DLL（ImageSharpなど）をToolsディレクトリから解決する。
    /// </summary>
    public class ToolPluginLoadContext : AssemblyLoadContext
    {
        private readonly string _pluginDirectory;

        public ToolPluginLoadContext(string name, string pluginDirectory)
            : base(name, isCollectible: true)
        {
            _pluginDirectory = pluginDirectory;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // プラグインディレクトリからDLLを探す
            var dllPath = Path.Combine(_pluginDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(dllPath))
            {
                return LoadFromAssemblyPath(dllPath);
            }

            // 見つからない場合はデフォルトコンテキストにフォールバック
            return null;
        }
    }
}
