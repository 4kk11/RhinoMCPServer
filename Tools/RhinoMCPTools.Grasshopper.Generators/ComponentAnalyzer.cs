// ComponentAnalyzer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp; // Keep this if analyzing syntax trees
using Microsoft.CodeAnalysis.CSharp.Syntax; // Keep this if analyzing syntax trees


namespace RhinoMCPTools.Grasshopper.Generators
{
    internal class ComponentAnalyzer
    {
        private readonly GeneratorExecutionContext _context;
        // フィルターに使用する型シンボルのリスト
        private readonly List<INamedTypeSymbol> _baseComponentSymbols;
        // Use SymbolEqualityComparer for reliable HashSet operations with symbols
        private readonly HashSet<INamedTypeSymbol> _componentTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        // フィルターに使用する型の完全修飾名リスト
        private static readonly string[] BaseComponentTypeNames = new[]
        {
            "Grasshopper.Kernel.GH_Component",
            "Grasshopper.Kernel.GH_Param`1", 
        };

        public ComponentAnalyzer(GeneratorExecutionContext context)
        {
            _context = context;
            _baseComponentSymbols = new List<INamedTypeSymbol>();

            // 各基底型のシンボルを取得
            foreach (var typeName in BaseComponentTypeNames)
            {
                var symbol = context.Compilation.GetTypeByMetadataName(typeName);
                if (symbol != null)
                {
                    _baseComponentSymbols.Add(symbol);
                    ComponentDiagnostics.Report(context, $"Found base component symbol: {typeName}");
                }
                else
                {
                    // シンボルが見つからない場合は警告を出す
                    ComponentDiagnostics.Report(context, $"Warning: Could not find {typeName} symbol.", DiagnosticSeverity.Warning);
                }
            }

            // どの型シンボルも見つからなかった場合は警告
            if (_baseComponentSymbols.Count == 0)
            {
                ComponentDiagnostics.Report(context, "Warning: No base component symbols were found.", DiagnosticSeverity.Warning);
            }
        }

        public IEnumerable<INamedTypeSymbol> FindAllGrasshopperComponents()
        {
            if (_baseComponentSymbols.Count == 0) return Enumerable.Empty<INamedTypeSymbol>();

            var complition = _context.Compilation;
            // Process all referenced assemblies
            foreach (var reference in complition.References)
            {
                // ComponentDiagnostics.Report(_context, $"Processing reference: {reference.Display}");
                ISymbol symbol = complition.GetAssemblyOrModuleSymbol(reference);
                if (symbol is IAssemblySymbol assemblySymbol)
                {
                    AnalyzeNamespace(assemblySymbol.GlobalNamespace);
                }
            }

            return _componentTypes;
        }

        private void AnalyzeNamespace(INamespaceSymbol namespaceSymbol)
        {
            foreach (var member in namespaceSymbol.GetMembers())
            {
                if (member is INamespaceSymbol nestedNamespace)
                {
                    AnalyzeNamespace(nestedNamespace); // 再帰的に探索
                }
                else if (member is INamedTypeSymbol typeSymbol)
                {
                    // GH_Componentを継承し、abstractでなく、publicで、
                    // publicなパラメータなしコンストラクタを持つクラスかチェック
                    if (typeSymbol.TypeKind == TypeKind.Class &&
                        !typeSymbol.IsAbstract &&
                        typeSymbol.DeclaredAccessibility == Accessibility.Public && // 通常コンポーネントはPublic
                        InheritsFromGHComponent(typeSymbol) &&
                        HasPublicParameterlessConstructor(typeSymbol) &&
                        !typeSymbol.Name.Contains("OBSOLETE")) // OBSOLETEという文字列を含む型名は除外
                    {
                        _componentTypes.Add(typeSymbol);
                    }
                }
            }
        }


        private bool InheritsFromGHComponent(INamedTypeSymbol typeSymbol)
        {
            var current = typeSymbol.BaseType; // 基底クラスからチェック開始
            while (current != null)
            {

                // 現在チェックしている基底クラスの元の型定義を取得
                // ジェネリック型の場合は <T> の状態 (未束縛)、非ジェネリックならそのまま
                var originalDefinition = current.IsGenericType ? current.OriginalDefinition : current;

                // いずれかの基底型と一致するかチェック
                foreach (var baseSymbol in _baseComponentSymbols)
                {
                    if (SymbolEqualityComparer.Default.Equals(originalDefinition, baseSymbol))
                    {
                        return true;
                    }
                }
                current = current.BaseType;
            }
            return false;
        }

        // 生成コードで new ClassName() を呼び出すため、
        // public なパラメータなしコンストラクタの存在を確認
        private bool HasPublicParameterlessConstructor(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.Constructors.Any(ctor =>
                !ctor.IsStatic && // 静的コンストラクタは除く
                ctor.Parameters.IsEmpty &&
                ctor.DeclaredAccessibility == Accessibility.Public);
        }
    }
}