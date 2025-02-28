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

        private static readonly string DaprJobsNameSpace = "Dapr.Jobs";
        private static readonly string DaprJobScheduleJobAsyncMethod = "ScheduleJobAsync";
        private static readonly string EndpointNameSpace = "Microsoft.AspNetCore.Builder";
        private static readonly string MapPostMethod = "MapPost";
        private static readonly string DaprJobInvocatoinUrlResource = "job";

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

            if (invocationExpression.Expression is not MemberAccessExpressionSyntax memberAccess)
            {
                return;
            }

            if (IsNamespaceAndMethodNameEqual(context, invocationExpression, DaprJobsNameSpace, DaprJobScheduleJobAsyncMethod))
            {
                var arguments = invocationExpression.ArgumentList.Arguments;
                if (arguments.Count > 0 && arguments[0].Expression is LiteralExpressionSyntax literal)
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
                .Where(invocation => IsNamespaceAndMethodNameEqual(context, invocation, EndpointNameSpace, MapPostMethod))
                .ToList();

            foreach (var mapping in endpointMappings)
            {
                // Look for route patterns like "/job/{jobname}"
                var argumentExpression = mapping.ArgumentList.Arguments.FirstOrDefault()?.Expression;
                string argument = string.Empty;
                if (argumentExpression is LiteralExpressionSyntax literal)
                {
                    argument = literal.Token.ValueText; // This extracts the string without quotes
                }

                // route patterns like "/job/{jobname} or /job/myJob"
                string[] endpointSplit = argument.Split('/');
                if (endpointSplit.Length == 3
                    && endpointSplit[1].Equals(DaprJobInvocatoinUrlResource)
                    && (endpointSplit[2].Equals(jobName) || ((endpointSplit[2].StartsWith("{") && endpointSplit[2].EndsWith("}")))))
                {
                    return;
                }
            }

            // If no matching route was found, report a diagnostic
            var diagnostic = Diagnostic.Create(DaprJobHandlerRule, default, jobName);
            context.ReportDiagnostic(diagnostic);
        }

        private static bool IsNamespaceAndMethodNameEqual(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, string symbolNamespace, string methodName)
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation.Expression);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            {
                return false;
            }

            // Check if the receiver is of type DaprJobsClient
            if (methodSymbol?.Name == methodName &&
                methodSymbol.ContainingNamespace.ToDisplayString() == symbolNamespace)
            {
                return true;
            }

            return false;
        }
    }
}
