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

namespace Dapr.VirtualActors.Analyzers;

/// <summary>
/// Diagnostic descriptors for the VirtualActors analyzers.
/// </summary>
internal static class AnalyzerDiagnostics
{
    public static readonly DiagnosticDescriptor TimerCallbackNotFound = new(
        "DAPRVACT002",
        "Timer callback method not found",
        "Timer callback method '{0}' does not exist on actor type '{1}'",
        "Dapr.VirtualActors",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ActorMethodMustReturnTask = new(
        "DAPRVACT004",
        "Actor method must return Task",
        "Actor interface method '{0}' must return Task or Task<T>",
        "Dapr.VirtualActors",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MissingRemindableInterface = new(
        "DAPRVACT005",
        "Actor uses reminders without IVirtualActorRemindable",
        "Actor type '{0}' registers reminders but does not implement IVirtualActorRemindable",
        "Dapr.VirtualActors",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
