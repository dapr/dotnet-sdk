// ------------------------------------------------------------------------
// Copyright 2021 The Dapr Authors
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

﻿using System;
using System.Text.Json;
using Dapr.Client;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dapr.AspNetCore.Test;

public class DaprMvcBuilderExtensionsTest
{
    [Fact]
    public void AddDapr_UsesSpecifiedDaprClientBuilderConfig()
    {
        var services = new ServiceCollection();

        var clientBuilder = new Action<DaprClientBuilder>(
            builder => builder.UseJsonSerializationOptions(
                new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = false
                }
            )
        );

        services.AddControllers().AddDapr(clientBuilder);

        var serviceProvider = services.BuildServiceProvider();

        DaprClientGrpc daprClient = serviceProvider.GetService<DaprClient>() as DaprClientGrpc;

        Assert.False(daprClient.JsonSerializerOptions.PropertyNameCaseInsensitive);
    }

    [Fact]
    public void AddDapr_UsesDefaultDaprClientBuilderConfig()
    {
        var services = new ServiceCollection();

        services.AddControllers().AddDapr();

        var serviceProvider = services.BuildServiceProvider();

        DaprClientGrpc daprClient = serviceProvider.GetService<DaprClient>() as DaprClientGrpc;

        Assert.True(daprClient.JsonSerializerOptions.PropertyNameCaseInsensitive);
    }

    [Fact]
    public void AddDapr_RegistersDaprOnlyOnce()
    {
        var services = new ServiceCollection();

        var clientBuilder = new Action<DaprClientBuilder>(
            builder => builder.UseJsonSerializationOptions(
                new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = false
                }
            )
        );

        // register with JsonSerializerOptions.PropertyNameCaseInsensitive = false
        services.AddControllers().AddDapr(clientBuilder);

        // register with JsonSerializerOptions.PropertyNameCaseInsensitive = true (default)
        services.AddControllers().AddDapr();

        var serviceProvider = services.BuildServiceProvider();

        DaprClientGrpc daprClient = serviceProvider.GetService<DaprClient>() as DaprClientGrpc;

        Assert.False(daprClient.JsonSerializerOptions.PropertyNameCaseInsensitive);
    }

#if NET8_0_OR_GREATER
    [Fact]
    public void AddDapr_WithKeyedServices()
    {
        var services = new ServiceCollection();

        services.AddKeyedSingleton("key1", new Object());

        services.AddControllers().AddDapr();

        var serviceProvider = services.BuildServiceProvider();

        var daprClient = serviceProvider.GetService<DaprClient>();

        Assert.NotNull(daprClient);
    }
#endif
}