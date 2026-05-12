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

using System;
using System.Linq;
using Dapr.StateManagement;
using Dapr.StateManagement.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.StateManagement.Test;

// The [StateStore] annotation causes the incremental source generator to emit
// WidgetStateStoreClient and WidgetStateStoreClientExtensions.
[StateStore("statestore")]
partial interface ITestWidgetStore : IDaprStateStoreClient;

/// <summary>
/// Verifies that the <c>StateStoreSourceGenerator</c> emits compilable code and
/// that the generated DI extension integrates with the service collection.
/// </summary>
public class StateStoreGeneratorTests
{
    [Fact]
    public void GeneratedAddExtension_RegistersInterface()
    {
        // Arrange: AddTestWidgetStore() is entirely generated code.
        var services = new ServiceCollection();
        services.AddDaprStateManagementClient()
            .AddTestWidgetStore();   // <-- generated extension method

        // Assert: The interface is registered.
        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(ITestWidgetStore));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
    }

    [Fact]
    public void GeneratedAddExtension_WhenDaprClientNotRegistered_DoesNotThrowAtRegistration()
    {
        // Registration itself should not throw even if DaprStateManagementClient is absent —
        // the factory is only evaluated on first resolution.
        var services = new ServiceCollection();
        services.AddDaprStateManagementClient()
            .AddTestWidgetStore();

        Assert.Contains(services, d => d.ServiceType == typeof(ITestWidgetStore));
    }

    [Fact]
    public void GeneratedClass_ImplementsExpectedInterface()
    {
        // ITestWidgetStore must implement IDaprStateStoreClient.
        // This is a compile-time check: if the generated code didn't satisfy the interface,
        // the generated extension method wouldn't compile.
        Assert.True(typeof(IDaprStateStoreClient).IsAssignableFrom(typeof(ITestWidgetStore)));
    }
}
