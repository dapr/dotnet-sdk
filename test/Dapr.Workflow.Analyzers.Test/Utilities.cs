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

using Microsoft.AspNetCore.Builder;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Immutable;
using System.Reflection;

namespace Dapr.Workflow.Analyzers.Test;

internal static class Utilities
{
    public static async Task<(ImmutableArray<Diagnostic> diagnostics, Document document, Workspace workspace)> GetDiagnosticsAdvanced(string code)
    {        
        var workspace = new AdhocWorkspace();

        // Create a new project with necessary references
        var project = workspace.AddProject("TestProject", LanguageNames.CSharp)
            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
            .AddMetadataReference(MetadataReference.CreateFromFile(typeof(object).Assembly.Location))
            .AddMetadataReferences(GetAllReferencesNeededForType(typeof(Workflow<,>)))
            .AddMetadataReferences(GetAllReferencesNeededForType(typeof(WebApplication)));

        // Add the document to the project
        var document = project.AddDocument("TestDocument.cs", code);

        // Get the syntax tree and create a compilation
        var syntaxTree = await document.GetSyntaxTreeAsync() ?? throw new InvalidOperationException("Syntax tree is null");
        var compilation = CSharpCompilation.Create("TestCompilation")
            .AddSyntaxTrees(syntaxTree)
            .AddReferences(project.MetadataReferences);

        var compilationWithAnalyzer = compilation.WithAnalyzers(
                ImmutableArray.Create<DiagnosticAnalyzer>(
                    new WorkflowRegistrationAnalyzer()));

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
