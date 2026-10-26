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

using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using Dapr.Testcontainers.Xunit.Attributes;

namespace Dapr.IntegrationTest.Vector;

/// <summary>Skips runtimes predating Vector, including the 1.19 RCs published before its merge.</summary>
public sealed class VectorFactAttribute : FactAttribute
{
    public VectorFactAttribute([CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = 0)
        : base(sourceFilePath, sourceLineNumber)
    {
        Skip = new MinimumDaprRuntimeFactAttribute("1.19").Skip;
        var version = Environment.GetEnvironmentVariable("DAPR_RUNTIME_VERSION")?.Trim();
        if (IsPreVectorReleaseCandidate(version))
            Skip = $"Vector is not included in Dapr {version}; use a Vector-capable runtime such as edge.";
    }

    internal static bool IsPreVectorReleaseCandidate(string? version) =>
        version is not null && Regex.IsMatch(version, @"^v?1\.19\.0-rc\.?0*[0-4](?:\+.*)?$", RegexOptions.IgnoreCase);
}

public sealed class VectorRuntimeGateTests
{
    [Theory]
    [InlineData("1.19.0-rc.4", true)]
    [InlineData("v1.19.0-rc04", true)]
    [InlineData("1.19.0-rc.3+build", true)]
    [InlineData("1.19.0-rc.5", false)]
    [InlineData("1.19.0", false)]
    [InlineData("edge", false)]
    [InlineData(null, false)]
    public void RecognizesReleaseCandidatesWithoutVector(string? version, bool expected) =>
        Assert.Equal(expected, VectorFactAttribute.IsPreVectorReleaseCandidate(version));
}
