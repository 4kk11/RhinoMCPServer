using System;
using System.Diagnostics;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;


namespace RhinoMCPTools.Grasshopper.Generators
{
    [Generator]
    public class ComponentToolSourceGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            // このソースジェネレータは構文通知を必要としない
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // Debugger.Launch();
            var analyzer = new ComponentAnalyzer(context);
            var componentTypes = analyzer.FindAllGrasshopperComponents();

            foreach (var componentType in componentTypes)
            {
                // Console.WriteLine($"Found component: {componentType.Name}"); 表示されない
                ComponentDiagnostics.Report(context, $"Found component: {componentType.Name}");
                var namespaceName = componentType.ContainingNamespace.ToDisplayString();
                var className = componentType.Name;
                var toolClassName = $"{className}Tool";

                var source = $@"
using System;
using System.Threading.Tasks;
using {namespaceName};

namespace GHComponentSourceGenerator
{{
    public class {toolClassName}
    {{
        public string Name => this._name;

        public string Description => this._description;

        private string _name;
        private string _description;

        public {toolClassName}()
        {{
            var component = new {className}();
            _name = component.Name;
            _description = component.Description;
        }}
    }}
}}";

                context.AddSource($"{toolClassName}.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }
    }
}