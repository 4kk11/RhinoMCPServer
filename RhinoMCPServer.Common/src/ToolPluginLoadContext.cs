using System;
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
            // Rhinoランタイムと共有するアセンブリはデフォルトコンテキストを使用
            // 隔離コンテキストでロードすると型同一性の問題が発生する
            // (例: System.Drawing.Size がRhino APIに渡せなくなる)
            if (ShouldUseDefaultContext(assemblyName.Name))
            {
                return null;
            }

            // プラグイン固有の依存DLL（ImageSharpなど）のみ隔離コンテキストでロード
            var dllPath = Path.Combine(_pluginDirectory, $"{assemblyName.Name}.dll");
            if (File.Exists(dllPath))
            {
                return LoadFromAssemblyPath(dllPath);
            }

            // 見つからない場合はデフォルトコンテキストにフォールバック
            return null;
        }

        /// <summary>
        /// 指定されたアセンブリをデフォルトコンテキストから使用すべきかを判定。
        /// Rhino APIとの型互換性が必要なアセンブリのみデフォルトコンテキストを使用。
        /// 注意: System.Text.JsonはMCP SDKが10.x必要なため隔離コンテキストで使用する。
        /// </summary>
        private static bool ShouldUseDefaultContext(string? assemblyName)
        {
            if (string.IsNullOrEmpty(assemblyName))
            {
                return false;
            }

            // System.Drawing系: Rhino APIとの型互換性が必要
            // (例: System.Drawing.Size, System.Drawing.Bitmap)
            if (assemblyName.StartsWith("System.Drawing", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            // Rhino関連アセンブリ: 型同一性が必要
            if (assemblyName.StartsWith("Rhino", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }
    }
}
