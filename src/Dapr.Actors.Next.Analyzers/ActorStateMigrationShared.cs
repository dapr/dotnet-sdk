using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;

namespace Dapr.Actors.Next.Roslyn;

internal static class ActorStateMigrationShared
{
    internal const string ShapeHashPrefix = "h1:";

    internal static bool TryParseNumericVersion(string typeName, out string canonicalName, out string version)
    {
        canonicalName = string.Empty;
        version = string.Empty;
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return false;
        }

        var i = typeName.Length - 1;
        while (i >= 0 && typeName[i] is >= '0' and <= '9')
        {
            i--;
        }

        if (i == typeName.Length - 1)
        {
            canonicalName = typeName;
            version = "0";
            return true;
        }

        if (i < 1 || typeName[i] != 'V')
        {
            return false;
        }

        canonicalName = typeName.Substring(0, i);
        version = typeName.Substring(i + 1);
        return canonicalName.Length > 0 && version.Length > 0;
    }

    internal static string ComputeShapeHash(INamedTypeSymbol symbol)
    {
        var builder = new StringBuilder();
        AppendType(builder, symbol, new HashSet<string>(StringComparer.Ordinal));
#if NETSTANDARD2_0
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(builder.ToString()));
#else
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
#endif
        return ShapeHashPrefix + ToLowerHex(bytes, 16);
    }

    internal static ImmutableArray<StateMember> GetSerializableMembers(INamedTypeSymbol type) =>
        type.GetMembers()
            .Where(static member => member is IPropertySymbol or IFieldSymbol)
            .Where(static member => member switch
            {
                IPropertySymbol property => !property.IsStatic && property.DeclaredAccessibility == Accessibility.Public && property.GetMethod is not null && property.Parameters.Length == 0,
                IFieldSymbol field => !field.IsStatic && field.DeclaredAccessibility == Accessibility.Public,
                _ => false,
            })
            .OrderBy(static member => member.Name, StringComparer.Ordinal)
            .Select(static member => member switch
            {
                IPropertySymbol property => new StateMember(
                    property.Name,
                    TypeName(property.Type),
                    property.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    property.SetMethod is { DeclaredAccessibility: Accessibility.Public },
                    property.IsRequired,
                    property.Type),
                IFieldSymbol field => new StateMember(
                    field.Name,
                    TypeName(field.Type),
                    field.Type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat),
                    !field.IsReadOnly,
                    field.IsRequired,
                    field.Type),
                _ => throw new InvalidOperationException("Unsupported member."),
            })
            .ToImmutableArray();

    internal static bool IsAdditiveStep(
        ImmutableArray<StateMember> fromMembers,
        ImmutableArray<StateMember> toMembers,
        bool toHasPublicParameterlessConstructor,
        out ImmutableArray<StateMember> copiedMembers)
    {
        copiedMembers = ImmutableArray<StateMember>.Empty;
        if (!toHasPublicParameterlessConstructor)
        {
            return false;
        }

        var fromByName = fromMembers.ToDictionary(static member => member.Name, StringComparer.Ordinal);
        var copied = ImmutableArray.CreateBuilder<StateMember>();
        foreach (var toMember in toMembers)
        {
            if (!fromByName.TryGetValue(toMember.Name, out var fromMember))
            {
                if (toMember.IsRequired)
                {
                    return false;
                }

                continue;
            }

            if (!StringComparer.Ordinal.Equals(fromMember.TypeName, toMember.TypeName) || !toMember.CanWrite)
            {
                return false;
            }

            copied.Add(toMember);
        }

        foreach (var fromMember in fromMembers)
        {
            if (!toMembers.Any(member => StringComparer.Ordinal.Equals(member.Name, fromMember.Name)))
            {
                return false;
            }
        }

        copiedMembers = copied.OrderBy(static member => member.Name, StringComparer.Ordinal).ToImmutableArray();
        return true;
    }

    internal static bool HasPublicParameterlessConstructor(INamedTypeSymbol type)
    {
        if (type.IsValueType)
        {
            return true;
        }

        return type.InstanceConstructors.Any(static ctor => !ctor.IsStatic && ctor.Parameters.Length == 0 && ctor.DeclaredAccessibility == Accessibility.Public);
    }

    internal static string TypeName(ITypeSymbol type) =>
        type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

    internal static string TypeIdentity(ITypeSymbol type)
    {
        var assembly = type.ContainingAssembly?.Identity.GetDisplayName();
        var name = type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        return string.IsNullOrEmpty(assembly) ? name : name + ", " + assembly;
    }

    private static void AppendType(StringBuilder builder, ITypeSymbol type, HashSet<string> seen)
    {
        var identity = TypeIdentity(type);
        builder.Append("type:").Append(identity).Append(';');
        if (!seen.Add(identity))
        {
            builder.Append("recursive;");
            return;
        }

        if (IsLeaf(type))
        {
            seen.Remove(identity);
            return;
        }

        if (type is INamedTypeSymbol named)
        {
            foreach (var member in GetSerializableMembers(named))
            {
                builder.Append("member:").Append(member.Name).Append(':');
                AppendMemberType(builder, member.SymbolType, seen);
                builder.Append(';');
            }
        }

        seen.Remove(identity);
    }

    private static void AppendMemberType(StringBuilder builder, ITypeSymbol type, HashSet<string> seen)
    {
        if (type is IArrayTypeSymbol array)
        {
            builder.Append("array[");
            AppendMemberType(builder, array.ElementType, seen);
            builder.Append(']');
            return;
        }

        if (type is INamedTypeSymbol { IsGenericType: true } generic)
        {
            builder.Append(TypeIdentity(generic.ConstructedFrom)).Append('<');
            foreach (var argument in generic.TypeArguments)
            {
                AppendMemberType(builder, argument, seen);
                builder.Append(',');
            }

            builder.Append('>');
            return;
        }

        AppendType(builder, type, seen);
    }

    private static bool IsLeaf(ITypeSymbol type) =>
        type.SpecialType is SpecialType.System_Boolean
            or SpecialType.System_Byte
            or SpecialType.System_SByte
            or SpecialType.System_Int16
            or SpecialType.System_UInt16
            or SpecialType.System_Int32
            or SpecialType.System_UInt32
            or SpecialType.System_Int64
            or SpecialType.System_UInt64
            or SpecialType.System_Single
            or SpecialType.System_Double
            or SpecialType.System_Char
            or SpecialType.System_String
            or SpecialType.System_Decimal
        || type.TypeKind == TypeKind.Enum
        || type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat) is "System.DateTime" or "System.DateTimeOffset" or "System.TimeSpan" or "System.Guid";

    private static string ToLowerHex(byte[] bytes, int length)
    {
        const string Hex = "0123456789abcdef";
        var chars = new char[length * 2];
        for (var i = 0; i < length; i++)
        {
            chars[i * 2] = Hex[bytes[i] >> 4];
            chars[i * 2 + 1] = Hex[bytes[i] & 0xF];
        }

        return new string(chars);
    }

    internal readonly record struct StateMember(
        string Name,
        string TypeName,
        string MetadataName,
        bool CanWrite,
        bool IsRequired,
        ITypeSymbol SymbolType);
}
