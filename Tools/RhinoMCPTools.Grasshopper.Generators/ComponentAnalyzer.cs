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
        private readonly INamedTypeSymbol _ghComponentSymbol;
        // Use SymbolEqualityComparer for reliable HashSet operations with symbols
        private readonly HashSet<INamedTypeSymbol> _componentTypes = new HashSet<INamedTypeSymbol>(SymbolEqualityComparer.Default);

        public ComponentAnalyzer(GeneratorExecutionContext context)
        {
            _context = context;
            // GH_Componentの型シンボルを取得
            _ghComponentSymbol = context.Compilation.GetTypeByMetadataName("Grasshopper.Kernel.GH_Component");

            // GH_Component シンボルが見つからない場合は警告を出すなどすると良い
            if (_ghComponentSymbol == null)
            {
                // ここで Diagnostic を報告することも検討
                Console.Error.WriteLine("Warning: Could not find Grasshopper.Kernel.GH_Component symbol.");
            }
        }

        public IEnumerable<INamedTypeSymbol> FindAllGrasshopperComponents()
        {
            if (_ghComponentSymbol == null) return Enumerable.Empty<INamedTypeSymbol>();

            var complition = _context.Compilation;
            // --- 参照アセンブリの分析を追加 ---
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
            // --- ここまで追加 ---

            // オプション: 現在のコンパイルのソースコードも分析する場合
            // (もし TestApp プロジェクト内でも GH_Component 派生クラスを定義するなら必要)
            /*
            foreach (var tree in _compilation.SyntaxTrees)
            {
                var semanticModel = _compilation.GetSemanticModel(tree);
                var root = tree.GetRoot();
                var classDeclarations = root.DescendantNodes().OfType<ClassDeclarationSyntax>();

                foreach (var classDeclaration in classDeclarations)
                {
                    if (semanticModel.GetDeclaredSymbol(classDeclaration) is INamedTypeSymbol typeSymbol &&
                        InheritsFromGHComponent(typeSymbol) &&
                        !typeSymbol.IsAbstract && // 具象クラスのみを対象
                        HasPublicParameterlessConstructor(typeSymbol)) // public パラメータなしコンストラクタが必要
                    {
                        _componentTypes.Add(typeSymbol);
                    }
                }
            }
            */

            return _componentTypes;
        }

        // --- AnalyzeNamespace メソッドを追加 ---
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
                    // ネストされた型も再帰的に探索する場合
                    // foreach (var nestedType in typeSymbol.GetTypeMembers().OfType<INamedTypeSymbol>())
                    // {
                    //     AnalyzeType(nestedType); // 必要であれば AnalyzeType のようなメソッドを実装
                    // }
                }
            }
        }
        // --- ここまで追加 ---


        private bool InheritsFromGHComponent(INamedTypeSymbol typeSymbol)
        {
            // _ghComponentSymbol のチェックは呼び出し元で行うのでここでは不要

            var current = typeSymbol.BaseType; // 基底クラスからチェック開始
            while (current != null)
            {
                // SymbolEqualityComparer を使って比較する
                if (SymbolEqualityComparer.Default.Equals(current, _ghComponentSymbol))
                {
                    return true;
                }
                current = current.BaseType;
            }
            return false;
        }

        // --- HasPublicParameterlessConstructor メソッドを追加 ---
        // 生成コードで new ClassName() を呼び出すため、
        // public なパラメータなしコンストラクタの存在を確認
        private bool HasPublicParameterlessConstructor(INamedTypeSymbol typeSymbol)
        {
            return typeSymbol.Constructors.Any(ctor =>
                !ctor.IsStatic && // 静的コンストラクタは除く
                ctor.Parameters.IsEmpty &&
                ctor.DeclaredAccessibility == Accessibility.Public);
        }
        // --- ここまで追加 ---
    }
}