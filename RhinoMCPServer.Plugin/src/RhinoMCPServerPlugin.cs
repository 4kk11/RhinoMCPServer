using System;
using System.IO;
using System.Reflection;
using Rhino;

namespace RhinoMCPServer.Plugin
{
    public class RhinoMCPServerPlugin : Rhino.PlugIns.PlugIn
    {
        public RhinoMCPServerPlugin()
        {
            Instance = this;
            Console.WriteLine("RhinoMCPServerPlugin");
            
            // アセンブリ解決イベントを設定
            AppDomain.CurrentDomain.AssemblyResolve += OnAssemblyResolve;
        }
        
        public static RhinoMCPServerPlugin? Instance { get; private set; }

        private Assembly? OnAssemblyResolve(object? sender, ResolveEventArgs args)
        {
            try
            {
                // 要求されたアセンブリの名前を取得
                var assemblyName = new AssemblyName(args.Name);
                
                // プラグインのインストールディレクトリを取得
                var pluginPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                if (pluginPath == null) return null;

                // Toolsディレクトリ内のDLLを探す
                var toolsDllPath = Path.Combine(pluginPath, "Tools", $"{assemblyName.Name}.dll");
                
                // DLLが存在する場合はロード
                if (File.Exists(toolsDllPath))
                {
                    Console.WriteLine($"Loading assembly from Tools directory: {toolsDllPath}");
                    return Assembly.LoadFrom(toolsDllPath);
                }

                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in assembly resolution: {ex.Message}");
                return null;
            }
        }
    }
}