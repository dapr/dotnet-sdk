// ------------------------------------------------------------------------
// Copyright 2025 The Dapr Authors
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ------------------------------------------------------------------------

using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;

namespace Dapr.Pubsub.Analyzers.Test;

internal static class Utilities
{
    public static async Task<(ImmutableArray<Diagnostic> diagnostics, Document document, Workspace workspace)> GetDiagnosticsAdvanced(string code)
    {
        var workspace = new AdhocWorkspace();

#if NET6_0
        var referenceAssemblies = ReferenceAssemblies.Net.Net60;
#elif NET7_0
        var referenceAssemblies = ReferenceAssemblies.Net.Net70;
#elif NET8_0
        var referenceAssemblies = ReferenceAssemblies.Net.Net80;
#elif NET9_0
        var referenceAssemblies = ReferenceAssemblies.Net.Net90;
#endif

        // Create a new project with necessary references
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.ConsoleApplication)
                .WithSpecificDiagnosticOptions(new Dictionary<string, ReportDiagnostic>
                {
                    { "CS1701", ReportDiagnostic.Suppress }
                }))
            //.AddMetadataReferences(await referenceAssemblies.ResolveAsync(LanguageNames.CSharp, default))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReferences(GetAllReferencesNeededForType(typeof(WebApplication)))
            .AddMetadataReferences(GetAllReferencesNeededForType(typeof(IHost)))
            .AddMetadataReferences(GetAllReferencesNeededForType(typeof(DaprEndpointConventionBuilderExtensions)));

        // Add the document to the project
        var document = project.AddDocument("TestDocument.cs", code);

        // Get the syntax tree and create a compilation
        var syntaxTree = await document.GetSyntaxTreeAsync() ?? throw new InvalidOperationException("Syntax tree is null");
        var compilation = CSharpCompilation.Create("TestCompilation")
            .AddSyntaxTrees(syntaxTree)
            .AddReferences(project.MetadataReferences)
            .WithOptions(project.CompilationOptions!);

        var compilationWithAnalyzer = compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new SubscriptionAnalyzer()));

        // Get diagnostics from the compilation
        var diagnostics = await compilationWithAnalyzer.GetAllDiagnosticsAsync();
        return (diagnostics, document, workspace);
    }

    public static MetadataReference[] GetAllReferencesNeededForType(Type type)
    {
        var files = GetAllAssemblyFilesNeededForType(type);

        return files.Select(x => MetadataReference.CreateFromFile(x)).Cast<MetadataReference>().ToArray();
    }

    private static ImmutableArray<string> GetAllAssemblyFilesNeededForType(Type type)
    {
        return type.Assembly.GetReferencedAssemblies()
            .Select(x => Assembly.Load(x.FullName))
            .Append(type.Assembly)
            .Select(x => x.Location)
            .ToImmutableArray();
    }
}
