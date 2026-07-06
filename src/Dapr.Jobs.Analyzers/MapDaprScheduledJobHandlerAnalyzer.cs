using System.Collections.Immutable;
using System.Resources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Jobs.Analyzers
{
    /// <summary>
    /// DaprJobsAnalyzer.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public sealed class MapDaprScheduledJobHandlerAnalyzer : DiagnosticAnalyzer
    {
        internal static readonly DiagnosticDescriptor DaprJobHandlerRule = new (
            id: "DAPR1501",
            title: new LocalizableResourceString(nameof(Resources.DAPR1501Title), Resources.ResourceManager, typeof(Resources)),
            messageFormat: new LocalizableResourceString(nameof(Resources.DAPR1501MessageFormat), Resources.ResourceManager, typeof(Resources)),
            category: "Usage",
            DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        private const string DaprJobsNameSpace = "Dapr.Jobs";
        private const string DaprJobScheduleJobAsyncMethod = "ScheduleJobAsync";
        private const string MethodNameSpace = "Dapr.Jobs.Extensions";
        private const string MapDaprScheduledJobHandlerMethod = "MapDaprScheduledJobHandler";

        /// <inheritdoc/>
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [DaprJobHandlerRule];

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

            if (!IsNamespaceAndMethodNameEqual(context, invocationExpression, DaprJobsNameSpace, DaprJobScheduleJobAsyncMethod))
            {
                return;
            }

            var arguments = invocationExpression.ArgumentList.Arguments;
            if (arguments.Count > 0 && arguments[0].Expression is LiteralExpressionSyntax literal)
            {
                string jobName = literal.Token.ValueText;

                // Now, we will check for a corresponding endpoint route.
                var jobNameLocation = invocationExpression.GetLocation();
                CheckForEndpointRoute(context, jobName, jobNameLocation);
            }
        }

        private static void CheckForEndpointRoute(SyntaxNodeAnalysisContext context, string jobName, Location jobNameLocation)
        {
            var root = context.SemanticModel.SyntaxTree.GetRoot();

            // Search for MapPost with the corresponding route
            var mapDaprScheduledJobHandlersCount = root
                .DescendantNodes()
                .OfType<InvocationExpressionSyntax>()
                .Count(invocation => IsNamespaceAndMethodNameEqual(context, invocation, MethodNameSpace, MapDaprScheduledJobHandlerMethod));

            if (mapDaprScheduledJobHandlersCount > 0)
            {
                return;
            }

            // If no matching route was found, report a diagnostic
            var diagnostic = Diagnostic.Create(DaprJobHandlerRule, jobNameLocation, jobName);
            context.ReportDiagnostic(diagnostic);
        }

        /// <summary>
        /// Determines whether a given method invocation matches a specified method name 
        /// within a given namespace.
        /// 
        /// For eg: MapDaprScheduledJobHandler (methodname) from the "Dapr.Jobs.Extension" (symbolNamespace) is being called.
        /// </summary>
        /// <param name="context">The syntax analysis context providing semantic information.</param>
        /// <param name="invocation">The invocation expression to analyze.</param>
        /// <param name="symbolNamespace">The expected namespace of the method.</param>
        /// <param name="methodName">The expected method name.</param>
        /// <returns>
        /// <c>true</c> if the method belongs to the specified namespace and has the expected name;
        /// otherwise, <c>false</c>.
        /// </returns>
        private static bool IsNamespaceAndMethodNameEqual(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, string symbolNamespace, string methodName)
        {
            var symbolInfo = context.SemanticModel.GetSymbolInfo(invocation.Expression);
            if (symbolInfo.Symbol is not IMethodSymbol methodSymbol)
            {
                return false;
            }

            // Check if the receiver is of type DaprJobsClient
            return methodSymbol.Name == methodName &&
                   methodSymbol.ContainingNamespace.ToDisplayString() == symbolNamespace;
        }
    }
}
