// using System;
// using System.Collections.Generic;
// using System.Diagnostics;
// using System.Linq;
// using System.Text;
// using Microsoft.CodeAnalysis;
// using Microsoft.CodeAnalysis.CSharp.Syntax;
// using Microsoft.CodeAnalysis.Text;

// namespace RhinoMCPTools.Grasshopper.Generators
// {
//     [Generator]
//     public class GrasshopperComponentToolGenerator : ISourceGenerator
//     {
//         public void Initialize(GeneratorInitializationContext context)
//         {
//             // デバッグのためにVisual Studioのプロセスにアタッチできるようにする
//             if (!Debugger.IsAttached)
//             {
//                 //Debugger.Launch();
//             }
            
//             // Register a syntax receiver that will be created for each generation pass
//             context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
//         }

//         public void Execute(GeneratorExecutionContext context)
//         {
//             try
//             {
//                 // デバッグ情報を出力
//                 context.ReportDiagnostic(Diagnostic.Create(
//                     new DiagnosticDescriptor(
//                         "GH001",
//                         "Source Generator Execution",
//                         "Source generator started execution",
//                         "Generation",
//                         DiagnosticSeverity.Info,
//                         isEnabledByDefault: true),
//                     Location.None));

//                 // Retrieve the populated receiver 
//                 if (!(context.SyntaxContextReceiver is SyntaxReceiver receiver))
//                 {
//                     context.ReportDiagnostic(Diagnostic.Create(
//                         new DiagnosticDescriptor(
//                             "GH002",
//                             "Receiver Error",
//                             "Syntax receiver was not of the expected type",
//                             "Generation",
//                             DiagnosticSeverity.Error,
//                             isEnabledByDefault: true),
//                         Location.None));
//                     return;
//                 }

//                 foreach (var componentClass in receiver.ComponentClasses)
//                 {
//                     var className = componentClass.Identifier.Text;
//                     var namespaceName = GetNamespace(componentClass);

//                     context.ReportDiagnostic(Diagnostic.Create(
//                         new DiagnosticDescriptor(
//                             "GH003",
//                             "Component Found",
//                             $"Processing component class: {className} in namespace: {namespaceName}",
//                             "Generation",
//                             DiagnosticSeverity.Info,
//                             isEnabledByDefault: true),
//                         Location.None));

//                     var toolClassName = $"Create{className}Tool";

//                     var source = $@"
// using System;
// using System.Linq;
// using System.Threading.Tasks;
// using System.Text.Json;
// using Grasshopper;
// using Grasshopper.Kernel;
// using ModelContextProtocol.Protocol.Types;
// using ModelContextProtocol.Server;
// using RhinoMCPServer.Common;
// using {namespaceName};
// using Rhino;

// namespace RhinoMCPTools.Grasshopper
// {{
//     public class {toolClassName} : IMCPTool
//     {{
//         public string Name => ""create_{className.ToLower()}"";
//         public string Description => ""Creates a {className} component on the Grasshopper canvas at the specified position."";

//         public JsonElement InputSchema => JsonSerializer.Deserialize<JsonElement>(@""{{
//             """"type"""": """"object"""",
//             """"properties"""": {{
//                 """"x"""": {{
//                     """"type"""": """"number"""",
//                     """"description"""": """"The x-coordinate position on the canvas"""",
//                     """"default"""": 0
//                 }},
//                 """"y"""": {{
//                     """"type"""": """"number"""",
//                     """"description"""": """"The y-coordinate position on the canvas"""",
//                     """"default"""": 0
//                 }}
//             }}
//         }}"");

//         public Task<CallToolResponse> ExecuteAsync(CallToolRequestParams request, IMcpServer? server)
//         {{
//             try
//             {{
//                 // Get position parameters
//                 var x = request.Arguments?.TryGetValue(""x"", out var xValue) == true ? 
//                     Convert.ToDouble(xValue.ToString()) : 0.0;
//                 var y = request.Arguments?.TryGetValue(""y"", out var yValue) == true ? 
//                     Convert.ToDouble(yValue.ToString()) : 0.0;

//                 // Get active Grasshopper document
//                 GH_Document? doc = Instances.ActiveDocument;
//                 if (doc == null)
//                 {{
//                     throw new McpServerException(""No active Grasshopper document found"");
//                 }}
                
//                 // Create and add component
//                 var component = new {className}();
//                 // Set component position
//                 component.Attributes.Pivot = new System.Drawing.PointF((float)x, (float)y);

//                 doc.AddObject(component, false);

//                 // Force complete solution recalculation to update UI
//                 component.ExpireSolution(true);
//                 RhinoApp.InvokeOnUiThread(() =>
//                 {{
//                     Instances.RedrawCanvas();
//                 }});

//                 var response = new
//                 {{
//                     status = ""success"",
//                     component = new
//                     {{
//                         guid = component.InstanceGuid.ToString(),
//                         name = component.Name,
//                         position = new
//                         {{
//                             x = x,
//                             y = y
//                         }}
//                     }}
//                 }};

//                 return Task.FromResult(new CallToolResponse()
//                 {{
//                     Content = [new Content() 
//                     {{ 
//                         Text = JsonSerializer.Serialize(response, new JsonSerializerOptions 
//                         {{ 
//                             WriteIndented = true 
//                         }}), 
//                         Type = ""text"" 
//                     }}]
//                 }});
//             }}
//             catch (Exception ex)
//             {{
//                 throw new McpServerException($""Error creating {className} component: {{ex.Message}}"", ex);
//             }}
//         }}
//     }}
// }}";

//                     context.AddSource($"{toolClassName}.g.cs", SourceText.From(source, Encoding.UTF8));

//                     context.ReportDiagnostic(Diagnostic.Create(
//                         new DiagnosticDescriptor(
//                             "GH004",
//                             "Tool Generated",
//                             $"Generated tool class: {toolClassName}",
//                             "Generation",
//                             DiagnosticSeverity.Info,
//                             isEnabledByDefault: true),
//                         Location.None));
//                 }
//             }
//             catch (Exception ex)
//             {
//                 context.ReportDiagnostic(Diagnostic.Create(
//                     new DiagnosticDescriptor(
//                         "GH005",
//                         "Generator Error",
//                         $"Error during source generation: {ex.Message}",
//                         "Generation",
//                         DiagnosticSeverity.Error,
//                         isEnabledByDefault: true),
//                     Location.None));
//             }
//         }

//         private static string GetNamespace(ClassDeclarationSyntax classDeclaration)
//         {
//             var namespaceDeclaration = classDeclaration.FirstAncestorOrSelf<NamespaceDeclarationSyntax>();
//             return namespaceDeclaration?.Name.ToString() ?? "DefaultNamespace";
//         }
//     }

//     /// <summary>
//     /// Created on demand before each generation pass
//     /// </summary>
//     class SyntaxReceiver : ISyntaxContextReceiver
//     {
//         public List<ClassDeclarationSyntax> ComponentClasses { get; } = new List<ClassDeclarationSyntax>();

//         /// <summary>
//         /// Called for every syntax node in the compilation, we can inspect the nodes and save any information useful for generation
//         /// </summary>
//         public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
//         {
//             try
//             {
//                 // Look for any class declaration
//                 if (context.Node is ClassDeclarationSyntax classDeclarationSyntax)
//                 {
//                     // Check if the class inherits from GH_Component
//                     if (context.SemanticModel.GetDeclaredSymbol(classDeclarationSyntax) is INamedTypeSymbol typeSymbol)
//                     {
//                         var baseType = typeSymbol.BaseType;
//                         while (baseType != null)
//                         {
//                             if (baseType.ToDisplayString() == "Grasshopper.Kernel.GH_Component")
//                             {
//                                 ComponentClasses.Add(classDeclarationSyntax);
//                                 break;
//                             }
//                             baseType = baseType.BaseType;
//                         }
//                     }
//                 }
//             }
//             catch (Exception)
//             {
//                 // Log exception if needed
//             }
//         }
//     }
// }