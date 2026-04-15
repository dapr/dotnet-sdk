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

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Dapr.VirtualActors.Generators.Test;

/// <summary>
/// Helper to create compilations for testing source generators.
/// </summary>
internal static class TestCompilationHelper
{
    /// <summary>
    /// Creates a CSharp compilation with the given source code and references to
    /// VirtualActors assemblies.
    /// </summary>
    public static CSharpCompilation CreateCompilation(params string[] sources)
    {
        var syntaxTrees = sources.Select(s => CSharpSyntaxTree.ParseText(s)).ToArray();

        // Gather references from the current runtime
        var references = new List<MetadataReference>
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Task).Assembly.Location),
        };

        // Add System.Runtime
        var runtimeDir = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var systemRuntimePath = Path.Combine(runtimeDir, "System.Runtime.dll");
        if (File.Exists(systemRuntimePath))
        {
            references.Add(MetadataReference.CreateFromFile(systemRuntimePath));
        }

        // Add VirtualActors assemblies
        references.Add(MetadataReference.CreateFromFile(typeof(VirtualActorId).Assembly.Location));
        references.Add(MetadataReference.CreateFromFile(typeof(Runtime.VirtualActor).Assembly.Location));

        return CSharpCompilation.Create(
            assemblyName: "TestAssembly",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Runs the given generator against a compilation and returns the results.
    /// </summary>
    public static GeneratorDriverRunResult RunGenerator<TGenerator>(CSharpCompilation compilation)
        where TGenerator : IIncrementalGenerator, new()
    {
        var generator = new TGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver = (CSharpGeneratorDriver)driver.RunGeneratorsAndUpdateCompilation(
            compilation, out var outputCompilation, out var diagnostics);
        return driver.GetRunResult();
    }
}
