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

namespace Dapr.Common.Generators.Models;

/// <summary>
/// Represents a single versioned variant of a Dapr gRPC method
/// (e.g., ScheduleJobAlpha1Async or ScheduleJobAsync).
/// </summary>
internal sealed class MethodVariant
{
    /// <summary>C# method name including the Async suffix, e.g. "ScheduleJobAlpha1Async".</summary>
    public string CSharpMethodName { get; init; } = string.Empty;

    /// <summary>Shared base name across all variants, e.g. "ScheduleJob".</summary>
    public string BaseName { get; init; } = string.Empty;

    /// <summary>Maturity suffix portion only, e.g. "Alpha1" or "" for stable.</summary>
    public string Suffix { get; init; } = string.Empty;

    /// <summary>Maturity tier (Stable, Beta, Alpha).</summary>
    public MaturityLevel Level { get; init; }

    /// <summary>Numeric discriminator within the tier (e.g. 1 for Alpha1, 2 for Alpha2).</summary>
    public int LevelNumber { get; init; }

    /// <summary>The gRPC method name on the service, e.g. "ScheduleJobAlpha1".</summary>
    public string GrpcMethodName { get; init; } = string.Empty;

    /// <summary>Fully-qualified gRPC method name, e.g. "dapr.proto.runtime.v1.Dapr/ScheduleJobAlpha1".</summary>
    public string FullyQualifiedMethodName { get; init; } = string.Empty;

    /// <summary>The Roslyn symbol for the method on DaprClient.</summary>
    public IMethodSymbol Symbol { get; init; } = null!;

    /// <summary>The request type for this variant.</summary>
    public INamedTypeSymbol RequestType { get; init; } = null!;

    /// <summary>The response type for this variant.</summary>
    public INamedTypeSymbol ResponseType { get; init; } = null!;
}
