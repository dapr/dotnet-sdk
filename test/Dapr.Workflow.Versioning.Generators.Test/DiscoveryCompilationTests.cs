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

namespace Dapr.Workflow.Versioning.Generators.Test;

public sealed class DiscoveryCompilationTests
{
    public static IEnumerable<object[]> Declarations()
    {
        var declarations = new[]
        {
            ("public class Outer {", "private sealed class Hidden", "}", false, false),
            ("public class Outer {", "protected sealed class Hidden", "}", false, false),
            ("public class Outer { private class Middle {", "public sealed class Hidden", "}}", false, false),
            ("", "file sealed class Hidden", "", false, false),
            ("file class Outer {", "public sealed class Hidden", "}", false, false),
            ("public class Outer<T> {", "public sealed class Hidden", "}", false, false),
            ("public class Outer<T> { public class Middle {", "public sealed class Hidden", "}}", false, false),
            ("public class Outer {", "public sealed class Hidden<T>", "}", false, false),
            ("public class Outer {", "public sealed class Visible", "}", true, true),
            ("internal class Outer {", "public sealed class Visible", "}", true, false),
            ("public class Outer {", "protected internal sealed class Visible", "}", true, false)
        };
        foreach (var (prefix, declaration, suffix, local, referenced) in declarations)
        foreach (var activity in new[] { false, true })
        foreach (var reference in new[] { false, true })
            yield return new object[] { prefix, declaration, suffix, activity, reference, reference ? referenced : local };
    }

    [Theory]
    [MemberData(nameof(Declarations))]
    public void DiscoveryOnlyEmitsAccessibleClosedTypes(string prefix, string declaration, string suffix,
        bool activity, bool reference, bool expected)
    {
        var source = $$"""
            using Dapr.Workflow;
            using System.Threading.Tasks;
            {{prefix}}
            {{declaration}} : {{(activity ? "WorkflowActivity" : "Workflow")}}<string, string>
            {
                public override Task<string> RunAsync({{(activity ? "WorkflowActivityContext" : "WorkflowContext")}} context, string input)
                    => Task.FromResult(input);
            }
            {{suffix}}
            """;
        var result = reference
            ? GeneratorTestHarness.Generate("public class Consumer { }", [GeneratorTestHarness.Compile(source).ToMetadataReference()], scanReferences: true)
            : GeneratorTestHarness.Generate(source);
        Assert.Equal(expected, result.GeneratedSource.Length > 0);
    }
}
