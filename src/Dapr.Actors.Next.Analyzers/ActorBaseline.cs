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
using Microsoft.CodeAnalysis.CSharp;

namespace Dapr.Actors.Next.Analyzers;

internal sealed class ActorBaseline
{
    internal const string ShippedFileName = "DaprActorsNext.Shipped.txt";
    internal const string UnshippedFileName = "DaprActorsNext.Unshipped.txt";

    private ActorBaseline(ImmutableDictionary<string, BaselineEntry> shipped)
    {
        Shipped = shipped;
    }

    internal ImmutableDictionary<string, BaselineEntry> Shipped { get; }

    internal static ActorBaseline Load(ImmutableArray<AdditionalText> additionalFiles, CancellationToken cancellationToken)
    {
        var builder = ImmutableDictionary.CreateBuilder<string, BaselineEntry>(StringComparer.Ordinal);

        foreach (var file in additionalFiles)
        {
            if (!StringComparer.OrdinalIgnoreCase.Equals(Path.GetFileName(file.Path), ShippedFileName))
            {
                continue;
            }

            var text = file.GetText(cancellationToken);
            if (text is null)
            {
                continue;
            }

            foreach (var line in text.Lines)
            {
                var value = line.ToString().Trim();
                if (value.Length == 0 || value.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }

                if (BaselineEntry.TryParse(value, out var entry))
                {
                    builder[entry.Key] = entry;
                }
            }
        }

        return new ActorBaseline(builder.ToImmutable());
    }
}

internal sealed class BaselineEntry
{
    private BaselineEntry(string kind, string name, string version, ImmutableDictionary<string, string> members, string originalLine)
    {
        Kind = kind;
        Name = name;
        Version = version;
        Members = members;
        OriginalLine = originalLine;
    }

    internal string Kind { get; }

    internal string Name { get; }

    internal string Version { get; }

    internal ImmutableDictionary<string, string> Members { get; }

    internal string OriginalLine { get; }

    internal string Key => Kind + "|" + Name;

    internal static bool TryParse(string line, out BaselineEntry entry)
    {
        entry = null!;
        var parts = line.Split('|');
        if (parts.Length < 4)
        {
            return false;
        }

        var members = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var member in parts[3].Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = member.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            members[member.Substring(0, separator)] = member.Substring(separator + 1);
        }

        entry = new BaselineEntry(parts[0], parts[1], parts[2], members.ToImmutable(), line);
        return true;
    }

    internal static BaselineEntry ForState(INamedTypeSymbol type)
    {
        var members = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var property in SerializableProperties(type))
        {
            members["P:" + property.Name] = property.Type.BaselineName();
        }

        return new BaselineEntry("state", type.BaselineName(), "v=1", members.ToImmutable(), string.Empty);
    }

    internal static BaselineEntry ForMigrationState(INamedTypeSymbol type)
    {
        var members = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        members["F:shapeHash"] = Dapr.Actors.Next.Roslyn.ActorStateMigrationShared.ComputeShapeHash(type);

        return new BaselineEntry("migration-state", type.BaselineName(), "v=1", members.ToImmutable(), string.Empty);
    }

    internal static BaselineEntry ForMigrationUpcaster(INamedTypeSymbol type)
    {
        var members = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        members["F:fingerprint"] = ComputeSyntaxFingerprint(type);

        return new BaselineEntry("migration-upcaster", type.BaselineName(), "v=1", members.ToImmutable(), string.Empty);
    }

    internal static BaselineEntry ForInterface(INamedTypeSymbol type)
    {
        var members = ImmutableDictionary.CreateBuilder<string, string>(StringComparer.Ordinal);
        foreach (var method in type.GetMembers().OfType<IMethodSymbol>().Where(static m => m.MethodKind == MethodKind.Ordinary))
        {
            members[MethodKey(method)] = method.ReturnType.BaselineName();
        }

        return new BaselineEntry("interface", type.BaselineName(), "v=1", members.ToImmutable(), string.Empty);
    }

    internal string ToBaselineLine()
    {
        var memberText = string.Join(";", Members.OrderBy(static p => p.Key, StringComparer.Ordinal).Select(static p => p.Key + "=" + p.Value));
        return string.Join("|", Kind, Name, Version, memberText);
    }

    internal static IEnumerable<IPropertySymbol> SerializableProperties(INamedTypeSymbol type) =>
        type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static p => !p.IsStatic && p.DeclaredAccessibility == Accessibility.Public && p.GetMethod is not null);

    private static string MethodKey(IMethodSymbol method)
    {
        var parameters = string.Join(",", method.Parameters.Select(static p => p.Type.BaselineName()));
        return "M:" + method.Name + "(" + parameters + ")";
    }

    private static string ComputeSyntaxFingerprint(INamedTypeSymbol type)
    {
        var text = string.Join("\n", type.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax().NormalizeWhitespace().ToFullString())
            .OrderBy(static value => value, StringComparer.Ordinal));
#if NETSTANDARD2_0
        using var sha = System.Security.Cryptography.SHA256.Create();
        var bytes = sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(text));
#else
        var bytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text));
#endif
        return "h1:" + ToLowerHex(bytes, 16);
    }

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
}
