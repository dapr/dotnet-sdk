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

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Dapr.Common.Generators.Tests.Helpers;

/// <summary>
/// Minimal in-memory implementation of <see cref="AnalyzerConfigOptions"/> for use in
/// source generator unit tests.
/// </summary>
internal sealed class MockAnalyzerConfigOptions(Dictionary<string, string>? values = null) : AnalyzerConfigOptions
{
    private readonly Dictionary<string, string> _values = values ?? [];

    public override bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? value)
        => _values.TryGetValue(key, out value);
}

/// <summary>
/// Minimal in-memory implementation of <see cref="AnalyzerConfigOptionsProvider"/> for use in
/// source generator unit tests. Supplies the same options for every tree and file.
/// </summary>
internal sealed class MockAnalyzerConfigOptionsProvider(Dictionary<string, string>? globalValues = null)
    : AnalyzerConfigOptionsProvider
{
    private readonly AnalyzerConfigOptions _globalOptions = new MockAnalyzerConfigOptions(globalValues);

    public override AnalyzerConfigOptions GlobalOptions => _globalOptions;
    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _globalOptions;
    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _globalOptions;
}
