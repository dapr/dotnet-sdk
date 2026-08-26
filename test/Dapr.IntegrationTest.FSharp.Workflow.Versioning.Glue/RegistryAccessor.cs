using System;
using System.Collections.Generic;
using Dapr.Workflow.Versioning;

namespace Dapr.IntegrationTest.FSharp.Workflow.Versioning.Glue;

/// <summary>
/// Public bridge exposing the internal generated workflow version registry to F# test consumers.
/// </summary>
public static class RegistryAccessor
{
    /// <summary>
    /// Gets the generated workflow version registry mapping canonical names to ordered workflow type names.
    /// </summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<string>> GetWorkflowVersionRegistry(IServiceProvider services)
        => GeneratedWorkflowVersionRegistry.GetWorkflowVersionRegistry(services);
}
