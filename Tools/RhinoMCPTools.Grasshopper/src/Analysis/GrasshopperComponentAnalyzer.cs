using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper;
using Grasshopper.Kernel;
using RhinoMCPServer.Common;
using ModelContextProtocol;

namespace RhinoMCPTools.Grasshopper.Analysis
{
    /// <summary>
    /// Grasshopperコンポーネントの解析と情報取得を行うクラス
    /// ComponentServer.ObjectProxies 経由で全コンポーネントを列挙する
    /// </summary>
    public class GrasshopperComponentAnalyzer
    {
        private static Dictionary<string, ComponentInfo>? _componentCache;
        private static Dictionary<Guid, ComponentInfo>? _guidIndex;
        private static bool _initialized;

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
            public Type? ComponentType { get; set; }
            public Guid ComponentGuid { get; set; }

            public IGH_DocumentObject CreateInstance()
            {
                // ComponentType が利用可能な場合は直接インスタンス化
                if (ComponentType != null)
                {
                    var instance = Activator.CreateInstance(ComponentType) as IGH_DocumentObject;
                    if (instance != null) return instance;
                }

                // ComponentServer のプロキシからインスタンスを作成
                var proxy = Instances.ComponentServer.EmitObject(ComponentGuid);
                if (proxy is IGH_DocumentObject docObj)
                {
                    return docObj;
                }

                throw new McpProtocolException($"Failed to create component '{Name}' (GUID: {ComponentGuid})");
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

        /// <summary>
        /// ComponentGuidからコンポーネント情報を取得
        /// </summary>
        public ComponentInfo? GetComponentByGuid(Guid componentGuid)
        {
            return _guidIndex?.GetValueOrDefault(componentGuid);
        }

        /// <summary>
        /// コンポーネント名（部分一致）で検索
        /// </summary>
        public IEnumerable<ComponentInfo> SearchByName(string searchTerm)
        {
            if (_componentCache == null) return Enumerable.Empty<ComponentInfo>();
            var lower = searchTerm.ToLowerInvariant();
            return _componentCache.Values
                .Where(c => c.Name.ToLowerInvariant().Contains(lower));
        }

        private void InitializeCache()
        {
            var newCache = new Dictionary<string, ComponentInfo>();
            var guidIndex = new Dictionary<Guid, ComponentInfo>();

            var proxies = Instances.ComponentServer.ObjectProxies;
            foreach (var proxy in proxies)
            {
                if (proxy.Obsolete) continue;

                var typeName = proxy.Type?.FullName ?? proxy.Desc.Name;
                if (string.IsNullOrEmpty(typeName) || newCache.ContainsKey(typeName))
                    continue;

                // GUIDの重複チェック
                if (guidIndex.ContainsKey(proxy.Guid))
                    continue;

                var isParam = proxy.Type != null && typeof(IGH_Param).IsAssignableFrom(proxy.Type);

                var info = new ComponentInfo
                {
                    Name = proxy.Desc.Name ?? string.Empty,
                    Description = proxy.Desc.Description ?? string.Empty,
                    FullTypeName = typeName,
                    IsParam = isParam,
                    Category = proxy.Desc.HasCategory ? (proxy.Desc.Category ?? "Unknown") : "Unknown",
                    SubCategory = proxy.Desc.HasSubCategory ? (proxy.Desc.SubCategory ?? "Unknown") : "Unknown",
                    ComponentType = proxy.Type,
                    ComponentGuid = proxy.Guid
                };

                newCache[typeName] = info;
                guidIndex[proxy.Guid] = info;
            }

            _componentCache = newCache;
            _guidIndex = guidIndex;
        }
    }
}
