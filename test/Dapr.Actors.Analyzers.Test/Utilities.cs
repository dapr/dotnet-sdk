// ------------------------------------------------------------------------
//  Copyright 2025 The Dapr Authors
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//      http://www.apache.org/licenses/LICENSE-2.0
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.
//  ------------------------------------------------------------------------

using System.Collections.Immutable;
using Dapr.Analyzers.Common;
using Microsoft.AspNetCore.Builder;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dapr.Actors.Analyzers.Test;

internal static class Utilities
{
    internal static ImmutableArray<DiagnosticAnalyzer> GetAnalyzers() =>
    [
        new ActorRegistrationAnalyzer(),
        new MappedActorHandlersAnalyzer(),
        new PreferActorJsonSerializationAnalyzer(),
        new TimerCallbackMethodPresentAnalyzer()
    ];
    
    internal static IReadOnlyList<MetadataReference> GetReferences()
    {
        var metadataReferences = TestUtilities.GetAllReferencesNeededForType(typeof(ActorRegistrationAnalyzer)).ToList();
        metadataReferences.AddRange(TestUtilities.GetAllReferencesNeededForType(typeof(MappedActorHandlersAnalyzer)));
        metadataReferences.AddRange(TestUtilities.GetAllReferencesNeededForType(typeof(PreferActorJsonSerializationAnalyzer)));
        metadataReferences.AddRange(TestUtilities.GetAllReferencesNeededForType(typeof(TimerCallbackMethodPresentAnalyzer)));
        metadataReferences.AddRange(TestUtilities.GetAllReferencesNeededForType(typeof(ActorsServiceCollectionExtensions)));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(object).Assembly.Location));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(WebApplication).Assembly.Location));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(IHost).Assembly.Location));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(ActorsEndpointRouteBuilderExtensions).Assembly.Location));
        metadataReferences.Add(MetadataReference.CreateFromFile(typeof(ActorsServiceCollectionExtensions).Assembly.Location));
        return metadataReferences;
    }
}
