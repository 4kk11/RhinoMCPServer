using System;
using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace RhinoMCPServer.Common
{
    /// <summary>
    /// Custom AssemblyLoadContext for loading MCP Host in isolation.
    /// This allows using System.Text.Json 10.x and ModelContextProtocol SDK
    /// without conflicting with Rhino8's built-in System.Text.Json 8.x.
    /// </summary>
    public class McpLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly string _basePath;

        /// <summary>
        /// Creates a new McpLoadContext for the specified assembly path.
        /// </summary>
        /// <param name="assemblyPath">Path to the main assembly to load (RhinoMCPServer.McpHost.dll)</param>
        public McpLoadContext(string assemblyPath) : base(name: "McpHostContext", isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(assemblyPath);
            _basePath = Path.GetDirectoryName(assemblyPath)!;
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // First, try to resolve using the dependency resolver
            // This uses the .deps.json file to find the correct version of dependencies
            string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);

            if (assemblyPath != null)
            {
                Console.WriteLine($"[McpLoadContext] Loading {assemblyName.Name} from {assemblyPath}");
                return LoadFromAssemblyPath(assemblyPath);
            }

            // If not found in deps.json, try to find in the base path
            var possiblePath = Path.Combine(_basePath, $"{assemblyName.Name}.dll");
            if (File.Exists(possiblePath))
            {
                Console.WriteLine($"[McpLoadContext] Loading {assemblyName.Name} from {possiblePath}");
                return LoadFromAssemblyPath(possiblePath);
            }

            // Fall back to default context for system assemblies
            Console.WriteLine($"[McpLoadContext] Falling back to default context for {assemblyName.Name}");
            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);

            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }

            return IntPtr.Zero;
        }
    }
}
