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
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Messaging.Analyzers;

/// <summary>
/// Reports DAPR1610 when the same (pubsub, topic) is registered with different <c>DeliveryMode</c>
/// values across one or more <c>[DaprTopic]</c> annotations.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateTopicDeliveryAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "DAPR1610";

    private const string DaprTopicAttributeFqn = "Dapr.Messaging.DaprTopicAttribute";
    private const string DeliveryModeFqn = "Dapr.Messaging.DeliveryMode";

    internal static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Duplicate topic with conflicting delivery modes",
        messageFormat: "Topic '{0}/{1}' is registered for both Streaming and Programmatic delivery; choose one delivery mode per topic",
        category: "Dapr.Messaging.Analyzers",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A topic must not be covered by both Streaming and Programmatic delivery modes.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(compilationStartContext =>
        {
            var topicAttr = compilationStartContext.Compilation.GetTypeByMetadataName(DaprTopicAttributeFqn);
            var deliveryMode = compilationStartContext.Compilation.GetTypeByMetadataName(DeliveryModeFqn);
            if (topicAttr is null || deliveryMode is null)
            {
                return;
            }

            var seen = new Dictionary<string, (string DeliveryMode, Location? Location)>(System.StringComparer.Ordinal);

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

                    var pubsub = attr.ConstructorArguments.ElementAtOrDefault(0).Value as string;
                    var topic = attr.ConstructorArguments.ElementAtOrDefault(1).Value as string;
                    if (string.IsNullOrEmpty(pubsub) || string.IsNullOrEmpty(topic))
                    {
                        continue;
                    }

                    var delivery = "Streaming";
                    foreach (var kvp in attr.NamedArguments)
                    {
                        if (kvp.Key == "Delivery")
                        {
                            delivery = kvp.Value.Value?.ToString() ?? "Streaming";
                        }
                    }

                    var key = $"{pubsub}/{topic}";
                    var location = ApplicationLocation(attr) ?? type.Locations.FirstOrDefault();
                    lock (seen)
                    {
                        if (seen.TryGetValue(key, out var existing) && !string.Equals(existing.DeliveryMode, delivery, System.StringComparison.Ordinal))
                        {
                            symbolContext.ReportDiagnostic(Diagnostic.Create(Rule, location, pubsub, topic));
                        }
                        else
                        {
                            seen[key] = (delivery, location);
                        }
                    }
                }
            }, SymbolKind.NamedType);
        });
    }

    private static Location? ApplicationLocation(AttributeData attr)
        => attr.ApplicationSyntaxReference?.GetSyntax().GetLocation();
}
