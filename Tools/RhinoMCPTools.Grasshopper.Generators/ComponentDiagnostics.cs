using Microsoft.CodeAnalysis;


namespace RhinoMCPTools.Grasshopper.Generators
{
    public static class ComponentDiagnostics
    {
        private static readonly DiagnosticDescriptor Discovery = new DiagnosticDescriptor(
            id: "SG001",
            title: "Source Generation Discovery",
            messageFormat: "{0}",
            category: "SourceGenerator",
            defaultSeverity: DiagnosticSeverity.Info, 
            isEnabledByDefault: true);

        public static void Report(
            GeneratorExecutionContext context,
            string message,
            DiagnosticSeverity severity = DiagnosticSeverity.Warning)
        {
            var descriptor = new DiagnosticDescriptor(
                Discovery.Id,
                Discovery.Title,
                Discovery.MessageFormat,
                Discovery.Category,
                severity,
                Discovery.IsEnabledByDefault);

            context.ReportDiagnostic(
                Diagnostic.Create(
                    descriptor,
                    Location.None,
                    message
                )
            );
        }
    }
}