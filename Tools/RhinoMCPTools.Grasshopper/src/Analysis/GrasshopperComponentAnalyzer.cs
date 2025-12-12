using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Grasshopper.Kernel;
using RhinoMCPServer.Common;
using ModelContextProtocol;

namespace RhinoMCPTools.Grasshopper.Analysis
{
    /// <summary>
    /// Grasshopperコンポーネントの解析と情報取得を行うクラス
    /// </summary>
    public class GrasshopperComponentAnalyzer
    {
        private static Dictionary<string, ComponentInfo>? _componentCache;
        private static bool _initialized;

        // フィルターに使用する型の完全修飾名リスト
        private static readonly string[] BaseComponentTypeNames = new[]
        {
            "Grasshopper.Kernel.GH_Component",
            "Grasshopper.Kernel.GH_Param`1",
        };

        /// <summary>
        /// コンポーネント情報を格納するクラス
        /// </summary>
        public class ComponentInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string FullTypeName { get; set; } = string.Empty;
            public bool IsParam { get; set; }
            public string Category { get; set; } = "Unknown";
            public string SubCategory { get; set; } = "Unknown";
            public Type ComponentType { get; set; } = null!;

            public IGH_DocumentObject CreateInstance()
            {
                var instance = Activator.CreateInstance(ComponentType) as IGH_DocumentObject;
                if (instance == null)
                {
                    throw new McpProtocolException($"Failed to create component of type '{FullTypeName}'");
                }
                return instance;
            }
        }

        /// <summary>
        /// コンストラクタで初期化を行います
        /// </summary>
        public GrasshopperComponentAnalyzer()
        {
            if (!_initialized)
            {
                InitializeCache();
                _initialized = true;
            }
        }

        /// <summary>
        /// キャッシュされたすべてのコンポーネント情報を取得します
        /// </summary>
        public IEnumerable<ComponentInfo> GetAllComponents()
        {
            return _componentCache?.Values ?? Enumerable.Empty<ComponentInfo>();
        }

        /// <summary>
        /// 型名から特定のコンポーネント情報を取得します
        /// </summary>
        public ComponentInfo? GetComponentByTypeName(string typeName)
        {
            return _componentCache?.GetValueOrDefault(typeName);
        }

        private void InitializeCache()
        {
            var newCache = new Dictionary<string, ComponentInfo>();
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic);

            foreach (var assembly in assemblies)
            {
                try
                {
                    foreach (var component in FindComponentsInAssembly(assembly))
                    {
                        if (!string.IsNullOrEmpty(component.FullTypeName))
                        {
                            newCache[component.FullTypeName] = component;
                        }
                    }
                }
                catch (Exception)
                {
                    // アセンブリの解析に失敗した場合は無視して続行
                    continue;
                }
            }

            _componentCache = newCache;
        }

        /// <summary>
        /// 指定されたアセンブリから利用可能なGrasshopperコンポーネントを検索します
        /// </summary>
        private IEnumerable<ComponentInfo> FindComponentsInAssembly(Assembly assembly)
        {
            var components = new List<ComponentInfo>();
            
            try
            {
                foreach (Type type in assembly.GetTypes())
                {
                    if (!IsValidComponentType(type))
                        continue;

                    try
                    {
                        var instance = Activator.CreateInstance(type) as IGH_DocumentObject;
                        if (instance == null)
                            continue;

                        var isParam = instance is IGH_Param;
                        
                        components.Add(new ComponentInfo
                        {
                            Name = instance.Name ?? string.Empty,
                            Description = instance.Description ?? string.Empty,
                            FullTypeName = type.FullName ?? string.Empty,
                            IsParam = isParam,
                            Category = instance.Category ?? "Unknown",
                            SubCategory = instance.SubCategory ?? "Unknown",
                            ComponentType = type
                        });
                    }
                    catch
                    {
                        // インスタンス作成に失敗した場合はスキップ
                        continue;
                    }
                }
            }
            catch (Exception ex)
            {
                throw new McpProtocolException($"Error analyzing assembly {assembly.FullName}: {ex.Message}", ex);
            }

            return components;
        }

        /// <summary>
        /// 指定された型がGrasshopperコンポーネントとして有効かチェックします
        /// </summary>
        private bool IsValidComponentType(Type type)
        {
            if (!type.IsClass || type.IsAbstract || type.IsGenericType)
                return false;

            if (type.GetConstructor(Type.EmptyTypes) == null)
                return false;

            if (type.Name.Contains("OBSOLETE") || 
                type.GetCustomAttribute<ObsoleteAttribute>() != null)
                return false;

            // 基底クラスをチェック
            var baseType = type;
            while ((baseType = baseType.BaseType) != null)
            {
                var baseName = baseType.IsGenericType 
                    ? baseType.GetGenericTypeDefinition().FullName 
                    : baseType.FullName;

                if (BaseComponentTypeNames.Contains(baseName))
                    return true;
            }

            return false;
        }
    }
}