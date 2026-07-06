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

using System.Reflection;

namespace Dapr.Workflow.Analyzers.Test;

public sealed class RoslynCompatibilityTests
{
    private static readonly Version MaximumSupportedRoslynReferenceVersion = new(4, 8, 0, 0);

    [Fact]
    public void ShippingAnalyzerShouldNotReferenceRoslynNewerThanSupportedConsumerCompiler()
    {
        var references = typeof(WorkflowTypeSafetyAnalyzer).Assembly.GetReferencedAssemblies()
            .Where(IsRoslynAssembly)
            .ToArray();

        Assert.NotEmpty(references);

        foreach (var reference in references)
        {
            Assert.True(
                reference.Version <= MaximumSupportedRoslynReferenceVersion,
                $"{typeof(WorkflowTypeSafetyAnalyzer).Assembly.GetName().Name} references {reference.Name} {reference.Version}, which is newer than {MaximumSupportedRoslynReferenceVersion}.");
        }
    }

    private static bool IsRoslynAssembly(AssemblyName reference) =>
        reference.Name?.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) == true;
}
