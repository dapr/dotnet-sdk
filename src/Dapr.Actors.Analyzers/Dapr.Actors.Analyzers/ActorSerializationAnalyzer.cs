// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Actors.Analyzers;

/// <summary>
/// Analyzes Dapr Actor classes and their interfaces for correct serialization attribute usage.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ActorSerializationAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Actor interface should inherit from IActor.</summary>
    public static readonly DiagnosticDescriptor ActorInterfaceMissingIActor = new(
        "DAPR1405",
        "Actor interface should inherit from IActor",
        "Interface '{0}' used by Actor class should inherit from IActor",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Interfaces implemented by Actor classes should inherit from IActor interface.");

    /// <summary>Enum members in Actor types should use EnumMember attribute.</summary>
    public static readonly DiagnosticDescriptor EnumMissingEnumMemberAttribute = new(
        "DAPR1406",
        "Enum members in Actor types should use EnumMember attribute",
        "Enum member '{0}' in enum '{1}' should be decorated with [EnumMember] attribute for proper serialization",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Enum members used in Actor types should use [EnumMember] attribute for consistent serialization.");

    /// <summary>Consider using JsonPropertyName for property name consistency.</summary>
    public static readonly DiagnosticDescriptor WeaklyTypedActorJsonPropertyRecommendation = new(
        "DAPR1407",
        "Consider using JsonPropertyName for property name consistency",
        "Property '{0}' in Actor class '{1}' should consider using [JsonPropertyName] attribute for consistent naming",
        "Usage",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        description: "Properties in Actor classes used with weakly-typed clients should consider [JsonPropertyName] attribute for consistent property naming.");

    /// <summary>Complex types used in Actor methods need serialization attributes.</summary>
    public static readonly DiagnosticDescriptor ComplexTypeInActorNeedsAttributes = new(
        "DAPR1408",
        "Complex types used in Actor methods need serialization attributes",
        "Type '{0}' used in Actor method should be decorated with [DataContract] and have [DataMember] on serializable properties",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Complex types used as parameters or return types in Actor methods should have proper serialization attributes.");

    /// <summary>Actor method parameter needs proper serialization attributes.</summary>
    public static readonly DiagnosticDescriptor ActorMethodParameterNeedsValidation = new(
        "DAPR1409",
        "Actor method parameter needs proper serialization attributes",
        "Parameter '{0}' of type '{1}' in method '{2}' should have proper serialization attributes",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Parameters in Actor methods should use types with proper serialization attributes for reliable data transfer.");

    /// <summary>Actor method return type needs proper serialization attributes.</summary>
    public static readonly DiagnosticDescriptor ActorMethodReturnTypeNeedsValidation = new(
        "DAPR1410",
        "Actor method return type needs proper serialization attributes",
        "Return type '{0}' in method '{1}' should have proper serialization attributes",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Return types in Actor methods should have proper serialization attributes for reliable data transfer.");

    /// <summary>Collection types in Actor methods need element type validation.</summary>
    public static readonly DiagnosticDescriptor CollectionTypeInActorNeedsElementValidation = new(
        "DAPR1411",
        "Collection types in Actor methods need element type validation",
        "Collection type '{0}' in Actor method contains elements of type '{1}' which needs proper serialization attributes",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Collection types used in Actor methods should contain elements with proper serialization attributes.");

    /// <summary>Record types should use DataContract and DataMember attributes for Actor serialization.</summary>
    public static readonly DiagnosticDescriptor RecordTypeNeedsDataContractAttributes = new(
        "DAPR1412",
        "Record types should use DataContract and DataMember attributes for Actor serialization",
        "Record '{0}' should be decorated with [DataContract] and have [DataMember] attributes on properties for proper Actor serialization",
        "Usage",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Record types used in Actor methods should have [DataContract] attribute and [DataMember] attributes on all properties for reliable serialization.");

    /// <summary>Actor class implementation should implement an interface that inherits from IActor.</summary>
    public static readonly DiagnosticDescriptor ActorClassMissingInterface = new(
        "DAPR1413",
        "Actor class implementation should implement an interface that inherits from IActor",
        "Actor class '{0}' should implement an interface that inherits from IActor",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Actor class implementations should implement an interface that inherits from IActor for proper Actor pattern implementation.");

    /// <summary>All types must either expose a public parameterless constructor or be decorated with the DataContractAttribute attribute.</summary>
    public static readonly DiagnosticDescriptor TypeMissingParameterlessConstructorOrDataContract = new(
        "DAPR1414",
        "All types must either expose a public parameterless constructor or be decorated with the DataContractAttribute attribute",
        "Type '{0}' must either have a public parameterless constructor or be decorated with [DataContract] attribute for proper serialization",
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "All types used in Actor methods must either expose a public parameterless constructor or be decorated with the DataContractAttribute attribute for reliable serialization.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [
            ActorInterfaceMissingIActor,
            EnumMissingEnumMemberAttribute,
            WeaklyTypedActorJsonPropertyRecommendation,
            ComplexTypeInActorNeedsAttributes,
            ActorMethodParameterNeedsValidation,
            ActorMethodReturnTypeNeedsValidation,
            CollectionTypeInActorNeedsElementValidation,
            RecordTypeNeedsDataContractAttributes,
            ActorClassMissingInterface,
            TypeMissingParameterlessConstructorOrDataContract
,
        ];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeClassDeclaration, SyntaxKind.ClassDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeInterfaceDeclaration, SyntaxKind.InterfaceDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeEnumDeclaration, SyntaxKind.EnumDeclaration);
    }

    private static void AnalyzeClassDeclaration(SyntaxNodeAnalysisContext context)
    {
        var classDeclaration = (ClassDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var classSymbol = semanticModel.GetDeclaredSymbol(classDeclaration);

        if (classSymbol == null)
        {
            return;
        }

        if (!InheritsFromActor(classSymbol))
        {
            return;
        }

        CheckActorInterfaces(context, classDeclaration, classSymbol);
        CheckActorClassImplementsIActorInterface(context, classDeclaration, classSymbol);
        CheckActorMethodTypes(context, classSymbol);
    }

    private static void AnalyzeInterfaceDeclaration(SyntaxNodeAnalysisContext context)
    {
        var interfaceDeclaration = (InterfaceDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var interfaceSymbol = semanticModel.GetDeclaredSymbol(interfaceDeclaration);

        if (interfaceSymbol == null)
        {
            return;
        }

        if (interfaceDeclaration.Identifier.ValueText.EndsWith("Actor") && !InheritsFromIActor(interfaceSymbol))
        {
            var diagnostic = Diagnostic.Create(
                ActorInterfaceMissingIActor,
                interfaceDeclaration.Identifier.GetLocation(),
                interfaceSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void AnalyzeEnumDeclaration(SyntaxNodeAnalysisContext context)
    {
        var enumDeclaration = (EnumDeclarationSyntax)context.Node;
        var semanticModel = context.SemanticModel;
        var enumSymbol = semanticModel.GetDeclaredSymbol(enumDeclaration);

        if (enumSymbol == null)
        {
            return;
        }

        foreach (var member in enumSymbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (HasAttribute(member, "EnumMemberAttribute", "EnumMember"))
            {
                continue;
            }

            var memberDeclaration = enumDeclaration.Members
                .FirstOrDefault(m => m.Identifier.ValueText == member.Name);

            if (memberDeclaration != null)
            {
                var diagnostic = Diagnostic.Create(
                    EnumMissingEnumMemberAttribute,
                    memberDeclaration.Identifier.GetLocation(),
                    member.Name,
                    enumSymbol.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static bool InheritsFromActor(INamedTypeSymbol classSymbol)
    {
        var baseType = classSymbol.BaseType;
        while (baseType != null)
        {
            if (baseType.Name == "Actor" && baseType.ContainingNamespace?.ToDisplayString() == "Dapr.Actors.Runtime")
            {
                return true;
            }

            baseType = baseType.BaseType;
        }

        return false;
    }

    private static bool InheritsFromIActor(INamedTypeSymbol interfaceSymbol)
    {
        return interfaceSymbol.AllInterfaces.Any(i =>
            i.Name == "IActor" && i.ContainingNamespace?.ToDisplayString() == "Dapr.Actors");
    }

    private static bool HasAttribute(ISymbol symbol, params string[] attributeNames)
    {
        return symbol.GetAttributes().Any(attr =>
            attributeNames.Contains(attr.AttributeClass?.Name) ||
            attributeNames.Contains(attr.AttributeClass?.MetadataName));
    }

    private static void CheckActorInterfaces(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration, INamedTypeSymbol classSymbol)
    {
        foreach (var interfaceType in classSymbol.Interfaces)
        {
            if (interfaceType.Name.EndsWith("Actor") && !InheritsFromIActor(interfaceType))
            {
                var diagnostic = Diagnostic.Create(
                    ActorInterfaceMissingIActor,
                    classDeclaration.Identifier.GetLocation(),
                    interfaceType.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }

    private static void CheckActorClassImplementsIActorInterface(SyntaxNodeAnalysisContext context, ClassDeclarationSyntax classDeclaration, INamedTypeSymbol classSymbol)
    {
        var implementsIActorInterface = classSymbol.Interfaces.Any(interfaceType => InheritsFromIActor(interfaceType));

        if (!implementsIActorInterface)
        {
            var diagnostic = Diagnostic.Create(
                ActorClassMissingInterface,
                classDeclaration.Identifier.GetLocation(),
                classSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    private static void CheckActorMethodTypes(SyntaxNodeAnalysisContext context, INamedTypeSymbol classSymbol)
    {
        var iActorInterfaces = classSymbol.AllInterfaces.Where(InheritsFromIActor).ToList();

        foreach (var interfaceMethod in iActorInterfaces.SelectMany(i => i.GetMembers().OfType<IMethodSymbol>()))
        {
            var implementation = classSymbol.FindImplementationForInterfaceMember(interfaceMethod) as IMethodSymbol;
            if (implementation == null)
            {
                continue;
            }

            if (!implementation.ReturnsVoid)
            {
                CheckMethodReturnType(context, implementation);
            }

            foreach (var parameter in implementation.Parameters)
            {
                CheckMethodParameter(context, implementation, parameter);
            }
        }
    }

    private static void CheckMethodReturnType(SyntaxNodeAnalysisContext context, IMethodSymbol method)
    {
        var returnType = method.ReturnType;
        var location = method.Locations.FirstOrDefault();

        if (location == null)
        {
            return;
        }

        if (returnType is INamedTypeSymbol namedReturnType &&
            namedReturnType.IsGenericType &&
            namedReturnType.Name == "Task" &&
            namedReturnType.TypeArguments.Length == 1)
        {
            returnType = namedReturnType.TypeArguments[0];
        }

        ValidateTypeForSerialization(context, returnType, location, method.Name, isParameter: false);
    }

    private static void CheckMethodParameter(SyntaxNodeAnalysisContext context, IMethodSymbol method, IParameterSymbol parameter)
    {
        var location = parameter.Locations.FirstOrDefault();
        if (location == null)
        {
            return;
        }

        ValidateTypeForSerialization(context, parameter.Type, location, method.Name, isParameter: true, parameter.Name);
    }

    private static void ValidateTypeForSerialization(SyntaxNodeAnalysisContext context, ITypeSymbol type, Location location, string methodName, bool isParameter, string? parameterName = null)
    {
        if (IsPrimitiveOrKnownType(type))
        {
            return;
        }

        if (IsCollectionType(type))
        {
            CheckCollectionElementType(context, type, location, methodName, isParameter, parameterName);
            return;
        }

        if (type is not INamedTypeSymbol namedType ||
            (namedType.TypeKind != TypeKind.Class && namedType.TypeKind != TypeKind.Struct))
        {
            return;
        }

        if (namedType.IsRecord)
        {
            CheckRecordSymbolForDataContractAttributes(context, namedType, location);
            return;
        }

        if (!HasParameterlessConstructorOrDataContract(namedType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                TypeMissingParameterlessConstructorOrDataContract,
                location,
                namedType.Name));
        }

        if (!HasProperSerializationAttributes(namedType))
        {
            if (isParameter)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ActorMethodParameterNeedsValidation,
                    location,
                    parameterName,
                    type.Name,
                    methodName));
            }
            else
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    ActorMethodReturnTypeNeedsValidation,
                    location,
                    type.Name,
                    methodName));
            }
        }
    }

    private static bool IsCollectionType(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType)
        {
            return false;
        }

        var collectionTypeNames = new[]
        {
            "IEnumerable", "ICollection", "IList", "IDictionary",
            "List", "Array", "Dictionary", "HashSet", "Queue", "Stack"
        };

        return collectionTypeNames.Any(name =>
            namedType.Name == name ||
            namedType.AllInterfaces.Any(i => i.Name == name)) ||
            type.TypeKind == TypeKind.Array;
    }

    private static void CheckCollectionElementType(SyntaxNodeAnalysisContext context, ITypeSymbol collectionType, Location location, string methodName, bool isParameter, string? parameterName)
    {
        ITypeSymbol? elementType = null;

        if (collectionType.TypeKind == TypeKind.Array && collectionType is IArrayTypeSymbol arrayType)
        {
            elementType = arrayType.ElementType;
        }
        else if (collectionType is INamedTypeSymbol namedType && namedType.IsGenericType)
        {
            elementType = namedType.TypeArguments.FirstOrDefault();
        }

        if (elementType == null || IsPrimitiveOrKnownType(elementType))
        {
            return;
        }

        if (elementType is INamedTypeSymbol namedElementType &&
            (namedElementType.TypeKind == TypeKind.Class || namedElementType.TypeKind == TypeKind.Struct) &&
            !HasProperSerializationAttributes(namedElementType))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                CollectionTypeInActorNeedsElementValidation,
                location,
                collectionType.Name,
                elementType.Name));
        }
    }

    private static bool HasProperSerializationAttributes(INamedTypeSymbol type)
    {
        return HasAttribute(type, "DataContractAttribute", "DataContract") ||
               HasAttribute(type, "SerializableAttribute", "Serializable") ||
               HasAttribute(type, "JsonObjectAttribute", "JsonObject") ||
               IsPrimitiveOrKnownType(type);
    }

    private static bool IsPrimitiveOrKnownType(ITypeSymbol type)
    {
        if (type.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        var typeName = type.ToDisplayString();
        var knownTypes = new[]
        {
            "byte", "sbyte", "short", "int", "long", "ushort", "uint", "ulong",
            "float", "double", "bool", "char", "decimal", "object", "string",
            "System.DateTime", "System.TimeSpan", "System.Guid", "System.Uri",
            "System.Xml.XmlQualifiedName", "System.Threading.Tasks.Task", "void"
        };

        return knownTypes.Contains(typeName) ||
               typeName.StartsWith("System.Threading.Tasks.Task<") ||
               typeName == "System.Void";
    }

    private static void CheckRecordSymbolForDataContractAttributes(SyntaxNodeAnalysisContext context, INamedTypeSymbol recordType, Location usageLocation)
    {
        if (!HasAttribute(recordType, "DataContractAttribute", "DataContract"))
        {
            context.ReportDiagnostic(Diagnostic.Create(
                RecordTypeNeedsDataContractAttributes,
                usageLocation,
                recordType.Name));
            return;
        }

        foreach (var property in recordType.GetMembers().OfType<IPropertySymbol>()
            .Where(p => p.DeclaredAccessibility == Accessibility.Public && !HasDataMemberAttribute(p)))
        {
            var propLocation = property.Locations.FirstOrDefault() ?? usageLocation;
            context.ReportDiagnostic(Diagnostic.Create(
                RecordTypeNeedsDataContractAttributes,
                propLocation,
                recordType.Name));
        }
    }

    private static bool HasDataMemberAttribute(IPropertySymbol property) =>
        HasAttribute(property, "DataMemberAttribute", "DataMember");

    private static bool HasParameterlessConstructorOrDataContract(INamedTypeSymbol type)
    {
        if (HasAttribute(type, "DataContractAttribute", "DataContract"))
        {
            return true;
        }

        var constructors = type.Constructors;

        if (!constructors.Any() && type.TypeKind == TypeKind.Class)
        {
            return true;
        }

        return constructors.Any(c =>
            c.DeclaredAccessibility == Accessibility.Public &&
            c.Parameters.Length == 0);
    }
}
