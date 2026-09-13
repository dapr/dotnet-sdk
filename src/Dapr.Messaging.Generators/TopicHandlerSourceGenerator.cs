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

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Dapr.Messaging.Generators.Models;
using Microsoft.CodeAnalysis;

namespace Dapr.Messaging.Generators;

/// <summary>
/// Incremental source generator that discovers <c>[DaprTopic]</c>-annotated
/// <c>ITopicHandler&lt;TMessage&gt;</c> (or <c>ITopicHandler&lt;TMessage, TResult&gt;</c>)
/// implementations and emits:
/// <list type="bullet">
///   <item>A per-handler <c>ITopicDispatcher</c> implementation.</item>
///   <item>An assembly-wide <c>IDaprMessagingSubscriberRegistry</c>.</item>
///   <item>A static subscription-manifest JSON string.</item>
///   <item>A source-generated <c>JsonSerializerContext</c> for all message types.</item>
///   <item>A <c>DaprMessagingGeneratedExtensions.AddDaprMessaging</c> DI extension.</item>
/// </list>
/// </summary>
[Generator]
public sealed class TopicHandlerSourceGenerator : IIncrementalGenerator
{
    private const string DaprTopicAttributeFqn = "Dapr.Messaging.DaprTopicAttribute";
    private const string DaprTopicMetadataAttributeFqn = "Dapr.Messaging.DaprTopicMetadataAttribute";
    private const string ITopicHandlerFqn = "Dapr.Messaging.ITopicHandler`1";
    private const string ITopicHandlerResultFqn = "Dapr.Messaging.ITopicHandler`2";

    private static readonly DiagnosticDescriptor Dapr1601 = new(
        id: "DAPR1601",
        title: "Handler must implement ITopicHandler<T>",
        messageFormat: "'{0}' is annotated with [DaprTopic] but does not implement ITopicHandler<T> or ITopicHandler<T, TResult>",
        category: "Dapr.Messaging.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Dapr1602 = new(
        id: "DAPR1602",
        title: "Handler must be a class",
        messageFormat: "'{0}' is annotated with [DaprTopic] but is not a class (records, structs, and interfaces are not supported)",
        category: "Dapr.Messaging.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Dapr1603 = new(
        id: "DAPR1603",
        title: "Duplicate topic subscription",
        messageFormat: "Topic '{0}/{1}' with delivery mode '{2}' is registered by more than one [DaprTopic]",
        category: "Dapr.Messaging.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Dapr1605 = new(
        id: "DAPR1605",
        title: "CEL match requires priority",
        messageFormat: "[DaprTopic] on '{0}' for topic '{1}/{2}' declares a Match expression but no Priority; priority is required for routing rules",
        category: "Dapr.Messaging.Generators",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Fast syntactic filter: only class declarations that have at least one attribute.
        var candidates = context.SyntaxProvider.CreateSyntaxProvider(
            predicate: static (node, _) => node is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax c && c.AttributeLists.Count > 0,
            transform: static (ctx, _) => BuildClassModel(ctx))
            .Where(static m => m is not null)!;

        var collected = candidates.Collect();

        context.RegisterSourceOutput(collected, static (spc, models) => Execute(spc, models));
    }

    // -----------------------------------------------------------------------
    //  Model construction
    // -----------------------------------------------------------------------

    private static ClassModel? BuildClassModel(GeneratorSyntaxContext ctx)
    {
        if (ctx.SemanticModel.GetDeclaredSymbol(ctx.Node) is not INamedTypeSymbol typeSymbol)
            return null;

        var topicAttrs = typeSymbol
            .GetAttributes()
            .Where(a => a.AttributeClass?.ToDisplayString() == DaprTopicAttributeFqn)
            .ToImmutableArray();

        if (topicAttrs.Length == 0)
            return null;

        return new ClassModel
        {
            TypeSymbol = typeSymbol,
            TopicAttributes = topicAttrs,
        };
    }

    // -----------------------------------------------------------------------
    //  Emission
    // -----------------------------------------------------------------------

    private static void Execute(SourceProductionContext context, ImmutableArray<ClassModel?> classModels)
    {
        var handlerModels = new List<TopicHandlerModel>();
        var messageFqns = new HashSet<string>();

        foreach (var cm in classModels)
        {
            if (cm is null)
                continue;
            var type = cm.TypeSymbol;

            // DAPR1602: must be a class.
            if (type.TypeKind != TypeKind.Class)
            {
                context.ReportDiagnostic(Diagnostic.Create(Dapr1602, type.Locations.FirstOrDefault(), type.ToDisplayString()));
                continue;
            }

            // Resolve TMessage (and TResult) from ITopicHandler<...>.
            if (!TryResolveMessageAndResult(type, out var messageFqn, out var isResponseVariant, out var resultFqn))
            {
                context.ReportDiagnostic(Diagnostic.Create(Dapr1601, type.Locations.FirstOrDefault(), type.ToDisplayString()));
                continue;
            }

            // Collect [DaprTopicMetadata] entries (class + assembly) for correlation.
            var classMetadata = CollectMetadataAttributes(type.GetAttributes());
            var assemblyMetadata = CollectMetadataAttributes(type.ContainingAssembly.GetAttributes());
            var allMetadata = MergeMetadata(assemblyMetadata, classMetadata);

            foreach (var attr in cm.TopicAttributes)
            {
                var model = BuildTopicModel(type, attr, messageFqn, isResponseVariant, resultFqn, allMetadata);
                if (model is null)
                    continue;

                // DAPR1605: Match requires Priority.
                if (!string.IsNullOrEmpty(model.Match) && model.Priority == 0)
                {
                    context.ReportDiagnostic(Diagnostic.Create(Dapr1605, ApplicationLocation(attr), type.ToDisplayString(), model.PubsubName, model.TopicName));
                    // keep emitting; the error halts the consumer build anyway.
                }

                handlerModels.Add(model);
                if (!string.IsNullOrEmpty(messageFqn))
                    messageFqns.Add(messageFqn);
            }
        }

        // DAPR1603: duplicate (pubsub, topic, delivery) tuples across distinct handler types.
        var seen = new HashSet<string>();
        foreach (var m in handlerModels)
        {
            var key = $"{m.PubsubName}/{m.TopicName}/{m.Delivery}";
            if (!seen.Add(key))
            {
                context.ReportDiagnostic(Diagnostic.Create(Dapr1603, null, m.PubsubName, m.TopicName, m.Delivery));
            }
        }

        var source = EmitSource(handlerModels, messageFqns);
        context.AddSource("DaprMessagingTopicHandlers.g.cs", Microsoft.CodeAnalysis.Text.SourceText.From(source, Encoding.UTF8));
    }

    private static TopicHandlerModel? BuildTopicModel(
        INamedTypeSymbol type,
        AttributeData attr,
        string messageFqn,
        bool isResponseVariant,
        string? resultFqn,
        Dictionary<string, string> allMetadata)
    {
        if (attr.ConstructorArguments.Length < 2)
            return null;
        var pubsub = attr.ConstructorArguments[0].Value as string;
        var topic = attr.ConstructorArguments[1].Value as string;
        if (string.IsNullOrEmpty(pubsub) || string.IsNullOrEmpty(topic))
            return null;

        string? match = null;
        string? route = null;
        int priority = 0;
        string? deadLetter = null;
        bool? enableRaw = null;
        bool bulk = false;
        int maxMessages = 100;
        int maxAwait = 1000;
        string delivery = "Streaming";
        string[]? metadataKeys = null;

        foreach (var kvp in attr.NamedArguments)
        {
            switch (kvp.Key)
            {
                case "Delivery": delivery = FormatEnumValue(kvp.Value) ?? "Streaming"; break;
                case "Match": match = kvp.Value.Value as string; break;
                case "Route": route = kvp.Value.Value as string; break;
                case "Priority": priority = (int)(kvp.Value.Value ?? 0); break;
                case "DeadLetterTopic": deadLetter = kvp.Value.Value as string; break;
                case "EnableRawPayload":
                    if (kvp.Value.Value is bool eb && eb)
                        enableRaw = eb;
                    break;
                case "BulkSubscribe": bulk = (bool)(kvp.Value.Value ?? false); break;
                case "MaxMessagesCount": maxMessages = (int)(kvp.Value.Value ?? 100); break;
                case "MaxAwaitDurationMs": maxAwait = (int)(kvp.Value.Value ?? 1000); break;
                case "MetadataKeys": metadataKeys = ToStringArray(kvp.Value); break;
            }
        }

        // Correlate metadata.
        var correlated = new Dictionary<string, string>();
        if (metadataKeys is { Length: > 0 })
        {
            foreach (var k in metadataKeys)
            {
                if (allMetadata.TryGetValue(k, out var v))
                    correlated[k] = v;
            }
        }
        else
        {
            foreach (var kvp in allMetadata)
                correlated[kvp.Key] = kvp.Value;
        }

        var safePubsub = Sanitize(pubsub!);
        var safeTopic = Sanitize(topic!);
        var dispatcherName = $"{type.Name}_{safePubsub}_{safeTopic}_Dispatcher";

        return new TopicHandlerModel
        {
            HandlerFqn = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            MessageFqn = messageFqn,
            IsResponseVariant = isResponseVariant,
            ResultFqn = resultFqn,
            PubsubName = pubsub!,
            TopicName = topic!,
            Route = string.IsNullOrWhiteSpace(route) ? topic! : route!,
            Delivery = delivery,
            Match = match,
            Priority = priority,
            DeadLetterTopic = deadLetter,
            EnableRawPayload = enableRaw,
            BulkSubscribe = bulk,
            MaxMessagesCount = maxMessages,
            MaxAwaitDurationMs = maxAwait,
            Metadata = correlated,
            DispatcherClassName = dispatcherName,
        };
    }

    private static bool TryResolveMessageAndResult(
        INamedTypeSymbol type,
        out string messageFqn,
        out bool isResponseVariant,
        out string? resultFqn)
    {
        messageFqn = string.Empty;
        resultFqn = null;
        isResponseVariant = false;

        foreach (var iface in type.AllInterfaces)
        {
            var def = iface.OriginalDefinition;
            if (def.ContainingNamespace?.ToDisplayString() != "Dapr.Messaging")
                continue;
            if (iface.IsGenericType)
            {
                var name = def.Name;
                if (name == "ITopicHandler" && iface.TypeArguments.Length == 2)
                {
                    messageFqn = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    resultFqn = iface.TypeArguments[1].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    isResponseVariant = true;
                    return true;
                }
            }
        }

        // fall back to the single-arg variant
        foreach (var iface in type.AllInterfaces)
        {
            var def = iface.OriginalDefinition;
            if (def.ContainingNamespace?.ToDisplayString() != "Dapr.Messaging")
                continue;
            if (iface.IsGenericType && def.Name == "ITopicHandler" && iface.TypeArguments.Length == 1)
            {
                messageFqn = iface.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                isResponseVariant = false;
                return true;
            }
        }

        return false;
    }

    private static Dictionary<string, string> CollectMetadataAttributes(ImmutableArray<AttributeData> attributes)
    {
        var dict = new Dictionary<string, string>();
        foreach (var attr in attributes)
        {
            if (attr.AttributeClass is null)
                continue;
            if (attr.AttributeClass.ToDisplayString() != DaprTopicMetadataAttributeFqn)
                continue;
            if (attr.ConstructorArguments.Length < 2)
                continue;
            var k = attr.ConstructorArguments[0].Value as string;
            var v = attr.ConstructorArguments[1].Value as string;
            if (!string.IsNullOrEmpty(k) && v is not null)
                dict[k!] = v;
        }
        return dict;
    }

    private static Dictionary<string, string> MergeMetadata(params Dictionary<string, string>[] sources)
    {
        var merged = new Dictionary<string, string>();
        foreach (var s in sources)
            foreach (var kvp in s)
                merged[kvp.Key] = kvp.Value;
        return merged;
    }

    private static string[]? ToStringArray(TypedConstant value)
    {
        if (value.Kind != TypedConstantKind.Array)
            return null;
        var list = new List<string>();
        foreach (var item in value.Values)
        {
            if (item.Value is string s)
                list.Add(s);
        }
        return list.Count == 0 ? null : list.ToArray();
    }

    /// <summary>
    /// Formats a <see cref="TypedConstant"/> holding an enum value as the enum member's simple name
    /// (e.g. <c>AppCallbackPush</c>), or <c>null</c> when the value cannot be resolved.
    /// </summary>
    private static string? FormatEnumValue(TypedConstant value)
    {
        if (value.Kind != TypedConstantKind.Enum || value.Value is null || value.Type is not INamedTypeSymbol enumType)
            return null;

        var underlying = System.Convert.ToInt64(value.Value);
        foreach (var member in enumType.GetMembers())
        {
            if (member is IFieldSymbol { IsConst: true } f && f.ConstantValue is { } cv)
            {
                if (System.Convert.ToInt64(cv) == underlying)
                    return f.Name;
            }
        }
        return null;
    }

    private static string Sanitize(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var c in s)
        {
            if (char.IsLetterOrDigit(c) || c == '_')
                sb.Append(c);
        }
        return sb.Length == 0 ? "x" : sb.ToString();
    }

    private static Location? ApplicationLocation(AttributeData attr)
        => attr.ApplicationSyntaxReference?.GetSyntax().GetLocation();

    // -----------------------------------------------------------------------
    //  Source emission
    // -----------------------------------------------------------------------

    private static string EmitSource(List<TopicHandlerModel> handlers, HashSet<string> messageFqns)
    {
        var sb = new StringBuilder();
        AppendHeader(sb);

        sb.AppendLine("using System;");
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Linq;");
        sb.AppendLine("using System.Text.Json;");
        sb.AppendLine("using System.Threading;");
        sb.AppendLine("using System.Threading.Tasks;");
        sb.AppendLine("using Dapr.Messaging;");
        sb.AppendLine("using Dapr.Messaging.PublishSubscribe;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection;");
        sb.AppendLine("using Microsoft.Extensions.DependencyInjection.Extensions;");
        sb.AppendLine();

        // JsonSerializerOptions holder. Source generators do not see other generators' output, so
        // we cannot rely on the System.Text.Json source generator to materialize a partial
        // JsonSerializerContext from emitted [JsonSerializable] attributes. This concrete holder is
        // runtime-backed; AOT source-gen of the context is a documented follow-up (consumers can
        // register their own JsonSerializerContext via DaprMessagingOptions).
        sb.AppendLine("internal sealed class DaprMessagingJsonContext");
        sb.AppendLine("{");
        sb.AppendLine("    public static readonly DaprMessagingJsonContext Default = new DaprMessagingJsonContext();");
        sb.AppendLine("    public JsonSerializerOptions Options { get; } = new JsonSerializerOptions(JsonSerializerDefaults.Web);");
        sb.AppendLine("}");
        sb.AppendLine();

        // Subscription manifest (JSON string, emitted as a regular escaped string literal so that
        // JSON quotes do not terminate the literal). Used for diagnostics; the AppCallback service
        // builds its proto response from the registry directly.
        sb.AppendLine("internal static class DaprMessagingSubscriptionManifest");
        sb.AppendLine("{");
        sb.Append("    public const string Json = \"");
        sb.Append(Escape(BuildManifestJson(handlers)));
        sb.AppendLine("\";");
        sb.AppendLine("}");
        sb.AppendLine();

        // Per-handler dispatchers.
        foreach (var m in handlers)
        {
            EmitDispatcher(sb, m);
        }

        // Assembly registry.
        EmitRegistry(sb, handlers);

        // DI extension.
        EmitGeneratedExtensions(sb, handlers);

        return sb.ToString();
    }

    private static void EmitDispatcher(StringBuilder sb, TopicHandlerModel m)
    {
        sb.AppendLine($"internal sealed class {m.DispatcherClassName} : ITopicDispatcher");
        sb.AppendLine("{");
        sb.AppendLine("    public TopicSubscriptionDescriptor Descriptor { get; } = new TopicSubscriptionDescriptor");
        sb.AppendLine("    {");
        sb.AppendLine($"        PubsubName = \"{Escape(m.PubsubName)}\",");
        sb.AppendLine($"        TopicName = \"{Escape(m.TopicName)}\",");
        sb.AppendLine($"        Route = \"{Escape(m.Route)}\",");
        sb.AppendLine($"        HandlerType = typeof({m.HandlerFqn}),");
        sb.AppendLine($"        MessageType = typeof({m.MessageFqn}),");
        sb.AppendLine($"        Delivery = global::Dapr.Messaging.DeliveryMode.{m.Delivery},");
        if (!string.IsNullOrEmpty(m.Match))
            sb.AppendLine($"        Match = \"{Escape(m.Match!)}\",");
        if (m.Priority != 0)
            sb.AppendLine($"        Priority = {m.Priority},");
        if (!string.IsNullOrEmpty(m.DeadLetterTopic))
            sb.AppendLine($"        DeadLetterTopic = \"{Escape(m.DeadLetterTopic!)}\",");
        if (m.EnableRawPayload is { } erp)
            sb.AppendLine($"        EnableRawPayload = {(erp ? "true" : "false")},");
        if (m.BulkSubscribe)
        {
            sb.AppendLine("        BulkSubscribe = new BulkSubscribeOptions");
            sb.AppendLine("        {");
            sb.AppendLine($"            Enabled = true,");
            sb.AppendLine($"            MaxMessagesCount = {m.MaxMessagesCount},");
            sb.AppendLine($"            MaxAwaitDurationMs = {m.MaxAwaitDurationMs},");
            sb.AppendLine("        },");
        }
        if (m.Metadata.Count > 0)
        {
            sb.AppendLine("        Metadata = new global::System.Collections.Generic.Dictionary<string, string>");
            sb.AppendLine("        {");
            foreach (var kvp in m.Metadata)
            {
                sb.AppendLine($"            [\"{Escape(kvp.Key)}\"] = \"{Escape(kvp.Value)}\",");
            }
            sb.AppendLine("        },");
        }
        sb.AppendLine("    };");
        sb.AppendLine();
        sb.AppendLine("    public async Task<TopicResponseAction> DispatchAsync(byte[] payload, TopicContext context, IServiceProvider serviceProvider, CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var handler = serviceProvider.GetRequiredService<{m.HandlerFqn}>();");
        sb.AppendLine($"        var message = JsonSerializer.Deserialize<{m.MessageFqn}>(payload, DaprMessagingJsonContext.Default.Options);");
        sb.AppendLine("        return await handler.HandleAsync(message!, context, ct);");
        sb.AppendLine("    }");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void EmitRegistry(StringBuilder sb, List<TopicHandlerModel> handlers)
    {
        sb.AppendLine("internal sealed class DaprMessagingSubscriberRegistry : IDaprMessagingSubscriberRegistry");
        sb.AppendLine("{");
        if (handlers.Count == 0)
        {
            sb.AppendLine("    private static readonly ITopicDispatcher[] _dispatchers = Array.Empty<ITopicDispatcher>();");
        }
        else
        {
            sb.AppendLine("    private static readonly ITopicDispatcher[] _dispatchers = new ITopicDispatcher[]");
            sb.AppendLine("    {");
            foreach (var m in handlers)
            {
                sb.AppendLine($"        new {m.DispatcherClassName}(),");
            }
            sb.AppendLine("    };");
        }
        sb.AppendLine();
        sb.AppendLine("    public IReadOnlyList<TopicSubscriptionDescriptor> Descriptors { get; } = _dispatchers.Select(d => d.Descriptor).ToArray();");
        sb.AppendLine();
        sb.AppendLine("    public ITopicDispatcher? Resolve(string pubsubName, string topicName, DeliveryMode mode) =>");
        sb.AppendLine("        _dispatchers.FirstOrDefault(d => d.Descriptor.PubsubName == pubsubName && d.Descriptor.TopicName == topicName && d.Descriptor.Delivery == mode);");
        sb.AppendLine("}");
        sb.AppendLine();
    }

    private static void EmitGeneratedExtensions(StringBuilder sb, List<TopicHandlerModel> handlers)
    {
        var hasSubscribers = handlers.Count > 0;
        var hasProgrammatic = handlers.Any(h => h.Delivery == "Programmatic" || h.Delivery == "1");
        var hasHttp = handlers.Any(h => h.Delivery == "Http" || h.Delivery == "2");

        sb.AppendLine("namespace Microsoft.Extensions.DependencyInjection");
        sb.AppendLine("{");
        sb.AppendLine("    /// <summary>");
        sb.AppendLine("    /// Source-generated dependency injection registration for the Dapr messaging stack.");
        sb.AppendLine("    /// </summary>");
        sb.AppendLine("    public static class DaprMessagingGeneratedExtensions");
        sb.AppendLine("    {");
        sb.AppendLine("        /// <summary>");
        sb.AppendLine("        /// Adds the complete Dapr messaging stack to the service collection: options, the publishing");
        sb.AppendLine("        /// client, and - when [DaprTopic]-annotated handlers are present in this assembly - the");
        sb.AppendLine("        /// generated dispatchers, subscriber registry, and the hosting services required by the");
        sb.AppendLine("        /// delivery modes those handlers declare.");
        sb.AppendLine("        /// </summary>");
        sb.AppendLine("        /// <param name=\"services\">The service collection.</param>");
        sb.AppendLine("        /// <param name=\"configure\">Optionally configures the Dapr messaging options.</param>");
        sb.AppendLine("        /// <returns>A builder allowing further Dapr messaging registration.</returns>");
        sb.AppendLine("        public static global::Dapr.Messaging.IDaprMessagingBuilder AddDaprMessaging(");
        sb.AppendLine("            this IServiceCollection services,");
        sb.AppendLine("            Action<global::Dapr.Messaging.DaprMessagingOptions>? configure = null)");
        sb.AppendLine("        {");
        sb.AppendLine("            // Options + the publishing surface are always registered.");
        sb.AppendLine("            global::Dapr.Messaging.DaprMessagingRegistration.AddPublisher(services, configure);");
        sb.AppendLine();

        if (hasSubscribers)
        {
            sb.AppendLine("            // Generated dispatchers and their handler types.");
            foreach (var m in handlers)
            {
                sb.AppendLine($"            services.AddSingleton<ITopicDispatcher>(sp => new {m.DispatcherClassName}());");
                sb.AppendLine($"            services.TryAddTransient<{m.HandlerFqn}>();");
            }
            sb.AppendLine("            services.TryAddSingleton(DaprMessagingJsonContext.Default);");
            sb.AppendLine();
        }

        // The registry is always registered so endpoint mapping and the AppCallback service can
        // resolve it even when this assembly declares no subscriptions.
        sb.AppendLine("            services.TryAddSingleton<IDaprMessagingSubscriberRegistry, DaprMessagingSubscriberRegistry>();");
        sb.AppendLine();

        if (hasProgrammatic)
        {
            sb.AppendLine("            // At least one subscription uses the programmatic (AppCallback push) delivery mode,");
            sb.AppendLine("            // so gRPC server hosting and the AppCallback service are registered.");
            sb.AppendLine("            global::Dapr.Messaging.DaprMessagingRegistration.AddProgrammaticSubscriptions(services);");
            sb.AppendLine();
        }

        if (hasHttp)
        {
            sb.AppendLine("            // At least one subscription uses HTTP delivery, so routing/endpoint support is registered.");
            sb.AppendLine("            global::Dapr.Messaging.DaprMessagingRegistration.AddHttpSubscriptions(services);");
            sb.AppendLine();
        }

        sb.AppendLine("            return global::Dapr.Messaging.DaprMessagingRegistration.CreateBuilder(services);");
        sb.AppendLine("        }");
        sb.AppendLine("    }");
        sb.AppendLine("}");
    }

    private static string BuildManifestJson(List<TopicHandlerModel> handlers)
    {
        var entries = handlers.Select(m =>
        {
            var deadLetter = string.IsNullOrEmpty(m.DeadLetterTopic) ? null : $",\"deadLetterTopic\":\"{m.DeadLetterTopic}\"";
            return $"{{\"pubsubname\":\"{m.PubsubName}\",\"topic\":\"{m.TopicName}\",\"delivery\":\"{m.Delivery}\"{deadLetter}}}";
        });
        return "[" + string.Join(",", entries) + "]";
    }

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static void AppendHeader(StringBuilder sb)
    {
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("// Generated by Dapr.Messaging.Generators.TopicHandlerSourceGenerator.");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
    }

    private sealed class ClassModel
    {
        public INamedTypeSymbol TypeSymbol { get; set; } = null!;
        public ImmutableArray<AttributeData> TopicAttributes { get; set; }
    }
}
