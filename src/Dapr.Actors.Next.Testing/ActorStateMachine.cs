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
using Dapr.Actors.Next.Abstractions;

namespace Dapr.Actors.Next.Testing;

/// <summary>
/// Structural-analysis entry point for actor state machines.
/// </summary>
public static class ActorStateMachine
{
    /// <summary>
    /// Analyzes a plain actor type and exposes the extension seam used by the future state-machine package.
    /// </summary>
    public static ActorStateMachineAnalysis Analyze<TActor>()
        where TActor : IActor
    {
        var actorType = typeof(TActor);
        var stateMachineAnalysis = TryAnalyzeStateMachine(actorType);
        if (stateMachineAnalysis is not null)
        {
            return new ActorStateMachineAnalysis(actorType, PublicMethods(actorType), stateMachineAnalysis);
        }

        return new ActorStateMachineAnalysis(actorType, PublicMethods(actorType), []);
    }

    private static IReadOnlyList<string>? TryAnalyzeStateMachine(Type actorType)
    {
        if (!DerivesFromStateMachine(actorType))
        {
            return null;
        }

        var analyzerType = Type.GetType("Dapr.Actors.Next.StateMachine.StateMachineAnalyzer, Dapr.Actors.Next.StateMachine", throwOnError: false);
        if (analyzerType is null)
        {
            return [$"State-machine actor '{actorType.FullName}' could not be analyzed because Dapr.Actors.Next.StateMachine is not loaded."];
        }

        var result = analyzerType.GetMethod("Analyze", BindingFlags.Public | BindingFlags.Static)!.Invoke(null, [actorType])!;
        var defects = (IReadOnlyList<string>)result.GetType().GetProperty("StructuralDefects")!.GetValue(result)!;
        return defects;
    }

    private static bool DerivesFromStateMachine(Type actorType)
    {
        for (var current = actorType; current is not null; current = current.BaseType!)
        {
            if (current.IsGenericType && string.Equals(current.GetGenericTypeDefinition().FullName, "Dapr.Actors.Next.StateMachine.StateMachineActor`2", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static IReadOnlyList<ActorReachableMethod> PublicMethods(Type actorType)
    {
        var methods = actorType
            .GetMethods(BindingFlags.Instance | BindingFlags.Public)
            .Where(method => method.DeclaringType != typeof(object) && !method.Name.StartsWith("InvokeOn", StringComparison.Ordinal))
            .Select(method => new ActorReachableMethod(method.Name, method.ReturnType, method.GetParameters().Select(parameter => parameter.ParameterType).ToArray()))
            .OrderBy(method => method.Name, StringComparer.Ordinal)
            .ToArray();

        return methods;
    }
}
