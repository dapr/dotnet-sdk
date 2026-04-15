// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
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

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Dapr.VirtualActors.Generators;

/// <summary>
/// Generates strongly-typed actor client implementations from <c>IVirtualActor</c> interfaces
/// annotated with <c>[GenerateActorClient]</c>.
/// </summary>
/// <remarks>
/// <para>
/// Generated clients wrap <c>IVirtualActorProxy</c> and provide type-safe method invocations
/// using the configured <c>IDaprSerializer</c>. No reflection is used — the generated code
/// is fully AOT-compatible.
/// </para>
/// <para>
/// Usage:
/// <code>
/// [GenerateActorClient]
/// public interface IMyActor : IVirtualActor
/// {
///     Task&lt;string&gt; GetGreetingAsync(string name, CancellationToken ct = default);
/// }
///
/// // Generated: MyActorClient class implementing IMyActor via IVirtualActorProxy
/// </code>
/// </para>
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class VirtualActorClientGenerator : IIncrementalGenerator
{
    private const string GenerateActorClientAttributeFullName = "Dapr.VirtualActors.Generators.GenerateActorClientAttribute";
    private const string ActorMethodAttributeFullName = "Dapr.VirtualActors.Generators.ActorMethodAttribute";

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Emit the marker attributes
        context.RegisterPostInitializationOutput(static ctx =>
        {
            ctx.AddSource(
                "Dapr.VirtualActors.Generators.GenerateActorClientAttribute.g.cs",
                SourceText.From(GenerateActorClientAttributeSource, Encoding.UTF8));

            ctx.AddSource(
                "Dapr.VirtualActors.Generators.ActorMethodAttribute.g.cs",
                SourceText.From(ActorMethodAttributeSource, Encoding.UTF8));
        });

        // Find interfaces with [GenerateActorClient]
        var clientDescriptors = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                GenerateActorClientAttributeFullName,
                predicate: static (_, _) => true,
                transform: static (gasc, _) => CreateDescriptor(gasc));

        // Generate client code for each
        context.RegisterSourceOutput(clientDescriptors, static (spc, descriptor) =>
        {
            if (descriptor is null)
                return;

            var source = GenerateClientSource(descriptor, spc);
            if (source is not null)
            {
                spc.AddSource($"{descriptor.Namespace}.{descriptor.ClientName}.g.cs", source);
            }
        });
    }

    private static ActorClientDescriptor? CreateDescriptor(GeneratorAttributeSyntaxContext context)
    {
        var attributeData = context.Attributes.SingleOrDefault();
        if (attributeData is null)
            return null;

        var interfaceSymbol = (INamedTypeSymbol)context.TargetSymbol;

        var namespaceName = attributeData.NamedArguments
            .SingleOrDefault(kvp => kvp.Key == "Namespace").Value.Value?.ToString()
            ?? interfaceSymbol.ContainingNamespace.ToDisplayString();

        var clientName = attributeData.NamedArguments
            .SingleOrDefault(kvp => kvp.Key == "Name").Value.Value?.ToString()
            ?? DeriveClientName(interfaceSymbol.Name);

        var methods = interfaceSymbol.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(m => m.MethodKind == MethodKind.Ordinary)
            .ToImmutableArray();

        return new ActorClientDescriptor(
            Namespace: namespaceName,
            ClientName: clientName,
            InterfaceFullName: interfaceSymbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            Accessibility: interfaceSymbol.DeclaredAccessibility,
            Methods: methods,
            TypeParameters: interfaceSymbol.TypeParameters,
            Compilation: context.SemanticModel.Compilation);
    }

    private static string DeriveClientName(string interfaceName) =>
        interfaceName.StartsWith("I", StringComparison.Ordinal) && interfaceName.Length > 1 && char.IsUpper(interfaceName[1])
            ? interfaceName.Substring(1) + "Client"
            : interfaceName + "Client";

    private static string? GenerateClientSource(ActorClientDescriptor descriptor, SourceProductionContext spc)
    {
        var cancellationTokenName = "System.Threading.CancellationToken";

        var sb = new StringBuilder(2048);
        sb.AppendLine("// <auto-generated />");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();

        // Accessibility
        var accessModifier = descriptor.Accessibility switch
        {
            Accessibility.Public => "public",
            Accessibility.Internal => "internal",
            _ => "internal",
        };

        // Type parameters
        var typeParams = descriptor.TypeParameters.Length > 0
            ? "<" + string.Join(", ", descriptor.TypeParameters.Select(tp => tp.Name)) + ">"
            : "";

        sb.AppendLine($"namespace {descriptor.Namespace}");
        sb.AppendLine("{");
        sb.AppendLine($"    {accessModifier} sealed class {descriptor.ClientName}{typeParams} : {descriptor.InterfaceFullName}{typeParams}");
        sb.AppendLine("    {");
        sb.AppendLine("        private readonly global::Dapr.VirtualActors.IVirtualActorProxy _proxy;");
        sb.AppendLine();
        sb.AppendLine($"        public {descriptor.ClientName}(global::Dapr.VirtualActors.IVirtualActorProxy proxy)");
        sb.AppendLine("        {");
        sb.AppendLine("            _proxy = proxy ?? throw new global::System.ArgumentNullException(nameof(proxy));");
        sb.AppendLine("        }");

        foreach (var method in descriptor.Methods)
        {
            sb.AppendLine();

            // Find CancellationToken parameter
            int ctIndex = -1;
            for (int i = 0; i < method.Parameters.Length; i++)
            {
                if (method.Parameters[i].Type.ToDisplayString() == cancellationTokenName)
                {
                    ctIndex = i;
                    break;
                }
            }

            // Validate: CT must be last
            if (ctIndex >= 0 && ctIndex != method.Parameters.Length - 1)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.CancellationTokenMustBeLast,
                    method.Locations.FirstOrDefault() ?? Location.None,
                    method.Name));
                return null;
            }

            // Validate: at most 1 data param (+ optional CT)
            var dataParams = method.Parameters.Where(p => p.Type.ToDisplayString() != cancellationTokenName).ToList();
            if (dataParams.Count > 1)
            {
                spc.ReportDiagnostic(Diagnostic.Create(
                    Diagnostics.TooManyParameters,
                    method.Locations.FirstOrDefault() ?? Location.None,
                    method.Name));
                return null;
            }

            // Get ActorMethod name override
            var actorMethodAttr = descriptor.Compilation.GetTypeByMetadataName(ActorMethodAttributeFullName);
            var remoteMethodName = method.Name;
            if (actorMethodAttr is not null)
            {
                var attr = method.GetAttributes()
                    .FirstOrDefault(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, actorMethodAttr));
                if (attr is not null)
                {
                    var nameOverride = attr.NamedArguments.FirstOrDefault(kvp => kvp.Key == "Name").Value.Value?.ToString();
                    if (!string.IsNullOrEmpty(nameOverride))
                        remoteMethodName = nameOverride!;
                }
            }

            // Build parameter list
            var paramList = new List<string>();
            foreach (var p in method.Parameters)
            {
                var typeName = p.Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                if (p.IsOptional && p.HasExplicitDefaultValue && p.ExplicitDefaultValue is null)
                    paramList.Add($"{typeName} {p.Name} = default");
                else
                    paramList.Add($"{typeName} {p.Name}");
            }

            var returnType = method.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            sb.AppendLine($"        public {returnType} {method.Name}({string.Join(", ", paramList)})");
            sb.AppendLine("        {");

            // Determine the proxy invocation
            var hasDataParam = dataParams.Count == 1;
            var dataParamName = hasDataParam ? dataParams[0].Name : null;
            var dataParamType = hasDataParam ? dataParams[0].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) : null;
            var ctParamName = ctIndex >= 0 ? method.Parameters[ctIndex].Name : null;

            // Check if method returns Task<T> or just Task
            var returnTypeSymbol = method.ReturnType as INamedTypeSymbol;
            var hasReturnValue = returnTypeSymbol?.TypeArguments.Length > 0;
            var returnValueType = hasReturnValue
                ? returnTypeSymbol!.TypeArguments[0].ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)
                : null;

            // Build invocation
            var methodNameLiteral = $"\"{remoteMethodName}\"";

            if (hasDataParam && hasReturnValue)
            {
                sb.Append($"            return _proxy.InvokeMethodAsync<{dataParamType}, {returnValueType}>({methodNameLiteral}, {dataParamName}");
            }
            else if (hasDataParam)
            {
                sb.Append($"            return _proxy.InvokeMethodAsync<{dataParamType}>({methodNameLiteral}, {dataParamName}");
            }
            else if (hasReturnValue)
            {
                sb.Append($"            return _proxy.InvokeMethodAsync<{returnValueType}>({methodNameLiteral}");
            }
            else
            {
                sb.Append($"            return _proxy.InvokeMethodAsync({methodNameLiteral}");
            }

            if (ctParamName is not null)
            {
                sb.Append($", {ctParamName}");
            }

            sb.AppendLine(");");
            sb.AppendLine("        }");
        }

        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    #region Attribute Source Templates

    private const string GenerateActorClientAttributeSource = @"// <auto-generated />
#nullable enable

namespace Dapr.VirtualActors.Generators
{
    /// <summary>
    /// Marks an IVirtualActor interface for automatic client code generation.
    /// The source generator will produce a strongly-typed client class that
    /// wraps IVirtualActorProxy for AOT-safe method invocation.
    /// </summary>
    [global::System.AttributeUsage(global::System.AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
    internal sealed class GenerateActorClientAttribute : global::System.Attribute
    {
        /// <summary>
        /// Optional custom name for the generated client class.
        /// Defaults to the interface name with the leading 'I' removed and 'Client' appended.
        /// </summary>
        public string? Name { get; set; }

        /// <summary>
        /// Optional namespace for the generated client class.
        /// Defaults to the namespace of the interface.
        /// </summary>
        public string? Namespace { get; set; }
    }
}
";

    private const string ActorMethodAttributeSource = @"// <auto-generated />
#nullable enable

namespace Dapr.VirtualActors.Generators
{
    /// <summary>
    /// Overrides the remote method name used when invoking an actor method via the Dapr sidecar.
    /// If not specified, the CLR method name is used.
    /// </summary>
    [global::System.AttributeUsage(global::System.AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    internal sealed class ActorMethodAttribute : global::System.Attribute
    {
        /// <summary>
        /// The remote method name to use for this actor method invocation.
        /// </summary>
        public string? Name { get; set; }
    }
}
";

    #endregion
}

internal sealed record ActorClientDescriptor(
    string Namespace,
    string ClientName,
    string InterfaceFullName,
    Accessibility Accessibility,
    ImmutableArray<IMethodSymbol> Methods,
    ImmutableArray<ITypeParameterSymbol> TypeParameters,
    Compilation Compilation);
