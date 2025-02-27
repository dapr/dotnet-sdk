using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Jobs.Analyzer
{
    /// <summary>
    /// DaprJobsAnalyzerAnalyzer.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DaprJobsAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        private static readonly DiagnosticDescriptor DaprJobHandlerRule = new DiagnosticDescriptor(
            id: "DAPRJOBS0001",
            title: "Ensure Post Mapper handler is present for all the Scheduled Jobs",
            messageFormat: "There should be a mapping post endpoint for each scheduled job to make sure app receives notifications for all the scheduled jobs",
            category: "Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(DaprJobHandlerRule); } }

        /// <inheritdoc/>
        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            context.RegisterSyntaxNodeAction(AnalyzeJobSchedulerHandler, SyntaxKind.InvocationExpression);
        }

        private static void AnalyzeJobSchedulerHandler(SyntaxNodeAnalysisContext context)
        {
            var invocationExpression = (InvocationExpressionSyntax)context.Node;

            var memberAccess = invocationExpression.Expression as MemberAccessExpressionSyntax;
            if (memberAccess?.Name.ToString() == "ScheduleJobAsync")
            {
                var arguments = invocationExpression.ArgumentList.Arguments;
                if (arguments.Count == 1 && arguments[0].Expression is LiteralExpressionSyntax literal)
                {
                    string jobName = literal.Token.ValueText;

                    // Now, we will check for a corresponding endpoint route.
                    CheckForEndpointRoute(context, jobName);
                }
            }
        }

        private static void CheckForEndpointRoute(SyntaxNodeAnalysisContext context, string jobName)
        {
            var root = context.SemanticModel.SyntaxTree.GetRoot();

            // Search for MapPost with the corresponding route
            var endpointMappings = root.DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.Expression.ToString().Contains("MapPost"))
                .ToList();

            foreach (var mapping in endpointMappings)
            {
                // Look for route patterns like "/job/{jobname}"
                var argument = mapping.ArgumentList.Arguments.FirstOrDefault()?.ToString();

                if (argument != null)
                {
                    string[] endpointSplit = argument.Split('/');
                    if (endpointSplit.Length == 3 
                        && endpointSplit[1].Equals("job") 
                        && (endpointSplit[2].Equals(jobName) || ((endpointSplit[2].StartsWith("{") && endpointSplit[2].EndsWith("}")))))
                    {
                        return;
                    }
                }
            }

            // If no matching route was found, report a diagnostic
            var diagnostic = Diagnostic.Create(DaprJobHandlerRule, context.Node.GetLocation(), jobName);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
