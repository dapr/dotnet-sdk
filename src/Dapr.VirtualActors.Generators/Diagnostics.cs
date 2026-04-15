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

namespace Dapr.VirtualActors.Generators;

/// <summary>
/// Diagnostic descriptors for the VirtualActors source generators.
/// </summary>
internal static class Diagnostics
{
    /// <summary>
    /// DAPRACT001: VirtualActor base type not found in compilation.
    /// </summary>
    public static readonly DiagnosticDescriptor VirtualActorBaseTypeNotFound = new(
        "DAPRACT001",
        "VirtualActor base type not found",
        "The source generator could not find the type '{0}'; ensure Dapr.VirtualActors.Runtime is referenced",
        "Dapr.VirtualActors",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// DAPRACT002: Count of discovered actor types.
    /// </summary>
    public static readonly DiagnosticDescriptor ActorDiscoveryCount = new(
        "DAPRACT002",
        "Actor discovery count",
        "Dapr VirtualActors discovered {0} actor type(s) for auto-registration",
        "Dapr.VirtualActors",
        DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    /// <summary>
    /// DAPRACT003: CancellationToken must be the last parameter.
    /// </summary>
    public static readonly DiagnosticDescriptor CancellationTokenMustBeLast = new(
        "DAPRACT003",
        "CancellationToken must be last parameter",
        "CancellationToken parameter must be the last parameter in actor method '{0}'",
        "Dapr.VirtualActors",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// DAPRACT004: Actor method must have at most one data parameter.
    /// </summary>
    public static readonly DiagnosticDescriptor TooManyParameters = new(
        "DAPRACT004",
        "Too many parameters",
        "Actor method '{0}' must have at most one data parameter optionally followed by CancellationToken",
        "Dapr.VirtualActors",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// DAPRACT005: Actor method must return Task or Task&lt;T&gt;.
    /// </summary>
    public static readonly DiagnosticDescriptor MethodMustReturnTask = new(
        "DAPRACT005",
        "Method must return Task",
        "Actor method '{0}' must return Task or Task<T>",
        "Dapr.VirtualActors",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
