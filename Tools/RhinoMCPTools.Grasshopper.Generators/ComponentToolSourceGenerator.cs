using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using RhinoMCPTools.Grasshopper.Generators.Templates;

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
            var analyzer = new ComponentAnalyzer(context);
            var componentTypes = analyzer.FindAllGrasshopperComponents();
        
            foreach (var componentType in componentTypes)
            {
                ComponentDiagnostics.Report(context, $"Found component: {componentType.Name}");
                var namespaceName = componentType.ContainingNamespace.ToDisplayString();
                var className = componentType.Name;
                var toolClassName = $"{className}Tool";
                var toolName = $"create_{className.ToLower()}_component";

                var source = ComponentToolTemplate.GetTemplate(namespaceName, className, toolClassName, toolName);
                context.AddSource($"{toolClassName}.g.cs", SourceText.From(source, Encoding.UTF8));
            }
        }
    }
}