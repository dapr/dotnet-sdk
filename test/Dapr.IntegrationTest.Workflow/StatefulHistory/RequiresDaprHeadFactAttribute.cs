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
//  ------------------------------------------------------------------------

using System.Runtime.CompilerServices;

namespace Dapr.IntegrationTest.Workflow.StatefulHistory;

/// <summary>
/// Marks a test that needs a daprd built from dapr master rather than a release, and skips it
/// otherwise.
/// </summary>
/// <remarks>
/// <para><see cref="Dapr.Testcontainers.Xunit.Attributes.MinimumDaprRuntimeFactAttribute"/> cannot
/// express this: the feature under test is on no release at all, and that attribute treats an
/// unset or non-semver <c>DAPR_RUNTIME_VERSION</c> (including <c>latest</c>) as satisfying the
/// minimum, which would run these tests against a sidecar that lacks the capability.</para>
/// <para>The tag matched here is the one the <c>integration-tests-dapr-head</c> CI job builds. Once
/// a dapr release ships the stateful-history protocol, this can be replaced by
/// <c>[MinimumDaprRuntimeFact("&lt;that version&gt;")]</c> and deleted.</para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class RequiresDaprHeadFactAttribute : FactAttribute
{
    private const string RuntimeVersionEnvVarName = "DAPR_RUNTIME_VERSION";

    /// <summary>The tag the dapr-head CI job builds and points DAPR_RUNTIME_VERSION at.</summary>
    public const string DaprHeadVersion = "dapr-head";

    /// <summary>
    /// Initializes the <see cref="RequiresDaprHeadFactAttribute"/> instance.
    /// </summary>
    /// <param name="sourceFilePath">Populated by the compiler; forwarded so xUnit v3 can report
    /// the test's source location (xUnit3003).</param>
    /// <param name="sourceLineNumber">Populated by the compiler; see
    /// <paramref name="sourceFilePath"/>.</param>
    public RequiresDaprHeadFactAttribute(
        [CallerFilePath] string? sourceFilePath = null,
        [CallerLineNumber] int sourceLineNumber = -1)
        : base(sourceFilePath, sourceLineNumber)
    {
        var current = Environment.GetEnvironmentVariable(RuntimeVersionEnvVarName);
        if (!string.Equals(current, DaprHeadVersion, StringComparison.Ordinal))
        {
            Skip = $"Requires a daprd built from dapr master ({RuntimeVersionEnvVarName}=" +
                   $"{DaprHeadVersion}); current: '{current ?? "<unset>"}'.";
        }
    }
}
