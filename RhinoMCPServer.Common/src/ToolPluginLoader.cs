using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;
using Rhino;

namespace RhinoMCPServer.Common
{
    public class ToolPluginLoader
    {
        private readonly Dictionary<string, IMCPTool> _loadedTools = new();
        private readonly string _pluginDirectory;
        private readonly List<AssemblyLoadContext> _loadContexts = new();

        public ToolPluginLoader(string pluginDirectory)
        {
            _pluginDirectory = pluginDirectory;
        }

        public IReadOnlyDictionary<string, IMCPTool> LoadedTools => _loadedTools;

        public void LoadPlugins()
        {
            // プラグインディレクトリ内のDLLファイルを検索
            var dllFiles = Directory.GetFiles(_pluginDirectory, "RhinoMCPTools.*.dll");

            foreach (var dllPath in dllFiles)
            {
                try
                {
                    LoadPlugin(dllPath);
                }
                catch (Exception ex)
                {
                    // ログ出力などのエラーハンドリング
                    Console.Error.WriteLine($"Failed to load plugin {dllPath}: {ex.Message}");
                }
            }
        }

        private void LoadPlugin(string dllPath)
        {
            // 各プラグインを独立したコンテキストでロード
            var loadContext = new AssemblyLoadContext(Path.GetFileNameWithoutExtension(dllPath), true);
            _loadContexts.Add(loadContext);

            using var fs = new FileStream(dllPath, FileMode.Open, FileAccess.Read);
            var assembly = loadContext.LoadFromStream(fs);

            // IMCPToolを実装している全ての型を検索
            var toolTypes = assembly.GetTypes()
                .Where(t => typeof(IMCPTool).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in toolTypes)
            {
                try
                {
                    // プラグインのインスタンスを作成
                    if (Activator.CreateInstance(type) is IMCPTool tool)
                    {
                        Console.WriteLine($"Loaded tool: {tool.Name} ({type.Name})");
                        _loadedTools[tool.Name] = tool;
                    }
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to create instance of tool {type.Name}: {ex.Message}");
                    RhinoApp.WriteLine($"Failed to create instance of tool {type.Name}: {ex.Message}");
                }
            }
        }

        public void UnloadPlugins()
        {
            _loadedTools.Clear();

            // 各アセンブリをアンロード
            foreach (var context in _loadContexts)
            {
                context.Unload();
            }
            _loadContexts.Clear();
        }
    }
}