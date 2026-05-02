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

#nullable enable
using System;
using System.Linq;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.AI.Microsoft.Extensions.Test;

public class DaprChatClientExtensionsTest
{
    #region Keyed overloads: AddDaprChatClient(services, serviceKey, componentName, ...)

    [Fact]
    public void AddDaprChatClient_Keyed_NullServices_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddDaprChatClient("key", "component"));
    }

    [Fact]
    public void AddDaprChatClient_Keyed_NullServiceKey_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddDaprChatClient((string)null!, "component"));
    }

    [Fact]
    public void AddDaprChatClient_Keyed_WhitespaceServiceKey_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() =>
            services.AddDaprChatClient("   ", "component"));
    }

    [Fact]
    public void AddDaprChatClient_Keyed_NullComponentName_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddDaprChatClient("key", (string)null!));
    }

    [Fact]
    public void AddDaprChatClient_Keyed_WhitespaceComponentName_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() =>
            services.AddDaprChatClient("key", "   "));
    }

    [Fact]
    public void AddDaprChatClient_Keyed_RegistersKeyedIChatClient()
    {
        var services = new ServiceCollection();
        services.AddDaprChatClient("my-key", "my-component");

        var descriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IChatClient) && object.Equals(s.ServiceKey, "my-key"));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void AddDaprChatClient_Keyed_DefaultLifetimeIsScoped()
    {
        var services = new ServiceCollection();
        services.AddDaprChatClient("my-key", "my-component");

        var descriptor = services.First(s =>
            s.ServiceType == typeof(IChatClient) && object.Equals(s.ServiceKey, "my-key"));
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void AddDaprChatClient_Keyed_ServiceLifetimeIsRespected(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        services.AddDaprChatClient("key", "component", lifetime);

        var descriptor = services.First(s =>
            s.ServiceType == typeof(IChatClient) && object.Equals(s.ServiceKey, "key"));
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    [Fact]
    public void AddDaprChatClient_KeyedWithConfigure_NullServices_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddDaprChatClient("key", "component", (Action<DaprChatClientOptions>?)null));
    }

    [Fact]
    public void AddDaprChatClient_KeyedWithConfigure_NullServiceKey_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddDaprChatClient((string)null!, "component", (Action<DaprChatClientOptions>?)null));
    }

    [Fact]
    public void AddDaprChatClient_KeyedWithConfigure_NullComponentName_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddDaprChatClient("key", (string)null!, (Action<DaprChatClientOptions>?)null));
    }

    [Fact]
    public void AddDaprChatClient_KeyedWithConfigure_RegistersKeyedIChatClient()
    {
        var services = new ServiceCollection();
        services.AddDaprChatClient("my-key", "my-component", opts => opts.ConversationComponentName = "override");

        var descriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IChatClient) && object.Equals(s.ServiceKey, "my-key"));
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void AddDaprChatClient_KeyedWithConfigure_NullConfigureIsAllowed()
    {
        var services = new ServiceCollection();
        services.AddDaprChatClient("key", "component", (Action<DaprChatClientOptions>?)null);

        var descriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IChatClient) && object.Equals(s.ServiceKey, "key"));
        Assert.NotNull(descriptor);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void AddDaprChatClient_KeyedWithConfigure_ServiceLifetimeIsRespected(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        services.AddDaprChatClient("key", "component", (Action<DaprChatClientOptions>?)null, lifetime);

        var descriptor = services.First(s =>
            s.ServiceType == typeof(IChatClient) && object.Equals(s.ServiceKey, "key"));
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    #endregion

    #region Non-keyed overloads: AddDaprChatClient(services, componentName, ...)
    // Overloads 3 (componentName, [lifetime]) and 4 (componentName, configure?, [lifetime]) share
    // the same validation logic. Overload 3 delegates to Overload 4, so tests target Overload 4
    // to resolve the C# overload ambiguity between them.

    [Fact]
    public void AddDaprChatClient_NonKeyed_NullServices_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddDaprChatClient("component", (Action<DaprChatClientOptions>?)null));
    }

    [Fact]
    public void AddDaprChatClient_NonKeyed_NullComponentName_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddDaprChatClient((string)null!, (Action<DaprChatClientOptions>?)null));
    }

    [Fact]
    public void AddDaprChatClient_NonKeyed_WhitespaceComponentName_ThrowsArgumentException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() =>
            services.AddDaprChatClient("   ", (Action<DaprChatClientOptions>?)null));
    }

    [Fact]
    public void AddDaprChatClient_NonKeyed_RegistersNonKeyedIChatClient()
    {
        var services = new ServiceCollection();
        services.AddDaprChatClient("my-component", (Action<DaprChatClientOptions>?)null);

        var descriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IChatClient) && s.ServiceKey == null);
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void AddDaprChatClient_NonKeyed_AlsoRegistersKeyedIChatClientUsingComponentName()
    {
        var services = new ServiceCollection();
        services.AddDaprChatClient("my-component", (Action<DaprChatClientOptions>?)null);

        var keyedDescriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IChatClient) && object.Equals(s.ServiceKey, "my-component"));
        Assert.NotNull(keyedDescriptor);
    }

    [Fact]
    public void AddDaprChatClient_NonKeyed_DefaultLifetimeIsScoped()
    {
        var services = new ServiceCollection();
        services.AddDaprChatClient("my-component", (Action<DaprChatClientOptions>?)null);

        var descriptor = services.First(s =>
            s.ServiceType == typeof(IChatClient) && s.ServiceKey == null);
        Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void AddDaprChatClient_NonKeyed_ServiceLifetimeIsRespected(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        // Use ServiceLifetime arg explicitly to resolve overload 3 unambiguously
        services.AddDaprChatClient("component", lifetime);

        var descriptor = services.First(s =>
            s.ServiceType == typeof(IChatClient) && s.ServiceKey == null);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    #endregion

    #region Configure-only overload: AddDaprChatClient(services, configure, [lifetime])

    [Fact]
    public void AddDaprChatClient_ConfigureOnly_NullServices_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            ((IServiceCollection)null!).AddDaprChatClient(opts => opts.ConversationComponentName = "x"));
    }

    [Fact]
    public void AddDaprChatClient_ConfigureOnly_NullConfigure_ThrowsArgumentNullException()
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentNullException>(() =>
            services.AddDaprChatClient((Action<DaprChatClientOptions>)null!));
    }

    [Fact]
    public void AddDaprChatClient_ConfigureOnly_RegistersNonKeyedIChatClient()
    {
        var services = new ServiceCollection();
        services.AddDaprChatClient(opts => opts.ConversationComponentName = "component");

        var descriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IChatClient) && s.ServiceKey == null);
        Assert.NotNull(descriptor);
    }

    [Fact]
    public void AddDaprChatClient_ConfigureOnly_DoesNotRegisterKeyedIChatClient()
    {
        var services = new ServiceCollection();
        services.AddDaprChatClient(opts => opts.ConversationComponentName = "component");

        var keyedDescriptor = services.FirstOrDefault(s =>
            s.ServiceType == typeof(IChatClient) && s.ServiceKey != null);
        Assert.Null(keyedDescriptor);
    }

    [Theory]
    [InlineData(ServiceLifetime.Singleton)]
    [InlineData(ServiceLifetime.Scoped)]
    [InlineData(ServiceLifetime.Transient)]
    public void AddDaprChatClient_ConfigureOnly_ServiceLifetimeIsRespected(ServiceLifetime lifetime)
    {
        var services = new ServiceCollection();
        services.AddDaprChatClient(opts => opts.ConversationComponentName = "component", lifetime);

        var descriptor = services.First(s =>
            s.ServiceType == typeof(IChatClient) && s.ServiceKey == null);
        Assert.Equal(lifetime, descriptor.Lifetime);
    }

    [Fact]
    public void AddDaprChatClient_ConfigureOnly_SecondCallDoesNotRegisterDuplicateDescriptor()
    {
        // TryAdd ensures only one descriptor is registered for the same service type
        var services = new ServiceCollection();
        services.AddDaprChatClient(opts => opts.ConversationComponentName = "component1");
        services.AddDaprChatClient(opts => opts.ConversationComponentName = "component2");

        var count = services.Count(s => s.ServiceType == typeof(IChatClient) && s.ServiceKey == null);
        Assert.Equal(1, count);
    }

    #endregion

    #region Return value (chaining)

    [Fact]
    public void AddDaprChatClient_NonKeyed_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddDaprChatClient("component", (Action<DaprChatClientOptions>?)null);
        Assert.Same(services, result);
    }

    [Fact]
    public void AddDaprChatClient_Keyed_ReturnsSameServiceCollection()
    {
        var services = new ServiceCollection();
        var result = services.AddDaprChatClient("key", "component");
        Assert.Same(services, result);
    }

    #endregion
}
