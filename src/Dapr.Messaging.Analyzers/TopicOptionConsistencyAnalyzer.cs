// ------------------------------------------------------------------------
// Copyright 2026 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//     http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Messaging.Analyzers;

/// <summary>
/// Reports DAPR1616 when a <c>[DaprTopic]</c> annotation opts into a feature without populating the
/// companion properties that feature requires, and DAPR1617 when it populates properties that are
/// ignored for the feature set or delivery mode that was selected.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TopicOptionConsistencyAnalyzer : DiagnosticAnalyzer
{
    /// <summary>The diagnostic id reported when a companion property is missing.</summary>
    public const string MissingOptionDiagnosticId = "DAPR1616";

    /// <summary>The diagnostic id reported when a property is populated but has no effect.</summary>
    public const string IgnoredOptionDiagnosticId = "DAPR1617";

    private const string DaprTopicAttributeFqn = "Dapr.Messaging.DaprTopicAttribute";

    internal static readonly DiagnosticDescriptor MissingOptionRule = new(
        id: MissingOptionDiagnosticId,
        title: "Incomplete [DaprTopic] configuration",
        messageFormat: "Topic '{0}/{1}' sets {2} but does not set {3}; the default value is used, which is likely unintended",
        category: "Dapr.Messaging.Analyzers",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Opting into a [DaprTopic] feature requires the companion properties for that feature to be populated explicitly.");

    internal static readonly DiagnosticDescriptor IgnoredOptionRule = new(
        id: IgnoredOptionDiagnosticId,
        title: "Ignored [DaprTopic] configuration",
        messageFormat: "Topic '{0}/{1}' sets {2}, which is ignored because {3}",
        category: "Dapr.Messaging.Analyzers",
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "A [DaprTopic] property that is not honored for the selected delivery mode or feature set should be removed.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
        => ImmutableArray.Create(MissingOptionRule, IgnoredOptionRule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStartContext =>
        {
            var topicAttr = compilationStartContext.Compilation.GetTypeByMetadataName(DaprTopicAttributeFqn);
            if (topicAttr is null)
            {
                return;
            }

            compilationStartContext.RegisterSymbolAction(symbolContext =>
            {
                if (symbolContext.Symbol is not INamedTypeSymbol type)
                {
                    return;
                }

                foreach (var attr in type.GetAttributes())
                {
                    if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, topicAttr))
                    {
                        continue;
                    }

                    Analyze(symbolContext, attr);
                }
            }, SymbolKind.NamedType);
        });
    }

    private static void Analyze(SymbolAnalysisContext context, AttributeData attr)
    {
        var pubsub = attr.ConstructorArguments.ElementAtOrDefault(0).Value as string;
        var topic = attr.ConstructorArguments.ElementAtOrDefault(1).Value as string;
        if (string.IsNullOrEmpty(pubsub) || string.IsNullOrEmpty(topic))
        {
            return;
        }

        var named = new Dictionary<string, TypedConstant>(StringComparer.Ordinal);
        foreach (var kvp in attr.NamedArguments)
        {
            named[kvp.Key] = kvp.Value;
        }

        var delivery = named.TryGetValue("Delivery", out var deliveryValue)
            ? deliveryValue.Value?.ToString() switch
            {
                "1" => "Programmatic",
                "2" => "Http",
                _ => "Streaming",
            }
            : "Streaming";

        var bulkEnabled = named.TryGetValue("BulkSubscribe", out var bulkValue) && bulkValue.Value is true;
        var hasMaxMessages = named.ContainsKey("MaxMessagesCount");
        var hasMaxAwait = named.ContainsKey("MaxAwaitDurationMs");
        var hasMatch = named.TryGetValue("Match", out var matchValue) && !string.IsNullOrWhiteSpace(matchValue.Value as string);
        var hasPriority = named.ContainsKey("Priority");
        var hasRoute = named.TryGetValue("Route", out var routeValue) && !string.IsNullOrWhiteSpace(routeValue.Value as string);

        // ---- DAPR1616: opted into a feature but left its companion properties at their defaults.
        if (bulkEnabled && (!hasMaxMessages || !hasMaxAwait))
        {
            var missing = (hasMaxMessages, hasMaxAwait) switch
            {
                (false, false) => "MaxMessagesCount or MaxAwaitDurationMs",
                (true, false) => "MaxAwaitDurationMs",
                _ => "MaxMessagesCount",
            };

            Report(context, MissingOptionRule, attr, "BulkSubscribe", pubsub!, topic!, "BulkSubscribe = true", missing);
        }

        // Match without Priority is already reported as an error (DAPR1605) by the source generator.

        // ---- DAPR1617: populated properties that the selected feature set or delivery mode ignores.
        if (!bulkEnabled && (hasMaxMessages || hasMaxAwait))
        {
            var property = hasMaxMessages && hasMaxAwait
                ? "MaxMessagesCount and MaxAwaitDurationMs"
                : hasMaxMessages ? "MaxMessagesCount" : "MaxAwaitDurationMs";

            Report(
                context,
                IgnoredOptionRule,
                attr,
                hasMaxMessages ? "MaxMessagesCount" : "MaxAwaitDurationMs",
                pubsub!,
                topic!,
                property,
                "BulkSubscribe is not enabled");
        }

        if (bulkEnabled && delivery == "Streaming")
        {
            Report(
                context,
                IgnoredOptionRule,
                attr,
                "BulkSubscribe",
                pubsub!,
                topic!,
                "BulkSubscribe",
                "bulk delivery is only supported for DeliveryMode.Programmatic and DeliveryMode.Http");
        }

        if (hasPriority && !hasMatch)
        {
            Report(context, IgnoredOptionRule, attr, "Priority", pubsub!, topic!, "Priority", "no Match expression is declared");
        }

        if (hasMatch && delivery != "Http")
        {
            Report(
                context,
                IgnoredOptionRule,
                attr,
                "Match",
                pubsub!,
                topic!,
                "Match",
                $"routing rules are only applied for DeliveryMode.Http, not DeliveryMode.{delivery}");
        }

        if (hasRoute && delivery != "Http")
        {
            Report(
                context,
                IgnoredOptionRule,
                attr,
                "Route",
                pubsub!,
                topic!,
                "Route",
                $"application routes are only used for DeliveryMode.Http, not DeliveryMode.{delivery}");
        }
    }

    private static void Report(
        SymbolAnalysisContext context,
        DiagnosticDescriptor rule,
        AttributeData attr,
        string propertyName,
        string pubsub,
        string topic,
        string subject,
        string reason)
    {
        var location = NamedArgumentLocation(attr, propertyName) ?? ApplicationLocation(attr);
        context.ReportDiagnostic(Diagnostic.Create(rule, location, pubsub, topic, subject, reason));
    }

    private static Location? ApplicationLocation(AttributeData attr)
        => attr.ApplicationSyntaxReference?.GetSyntax().GetLocation();

    private static Location? NamedArgumentLocation(AttributeData attr, string propertyName)
    {
        if (attr.ApplicationSyntaxReference?.GetSyntax() is not AttributeSyntax syntax || syntax.ArgumentList is null)
        {
            return null;
        }

        foreach (var argument in syntax.ArgumentList.Arguments)
        {
            if (argument.NameEquals?.Name.Identifier.ValueText == propertyName)
            {
                return argument.GetLocation();
            }
        }

        return null;
    }
}
