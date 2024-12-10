// ------------------------------------------------------------------------
// Copyright 2024 The Dapr Authors
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
using System.Collections.Generic;
using System.Net.Http;
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.AI.Test.Conversation.Extensions;

public class DaprAiConversationBuilderExtensionsTest
{
    [Fact]
    public void AddDaprConversationClient_FromIConfiguration()
    {
        const string apiToken = "abc123";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string> { { "DAPR_API_TOKEN", apiToken } })
            .Build();
        
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddDaprAiConversation();

        var app = services.BuildServiceProvider();

        var conversationClient = app.GetRequiredService<DaprConversationClient>() as DaprConversationClient;
        
        Assert.NotNull(conversationClient!.DaprApiToken);
        Assert.Equal(apiToken, conversationClient.DaprApiToken);
    }
    
    [Fact]
    public void AddDaprAiConversation_WithoutConfigure_ShouldAddServices()
    {
        var services = new ServiceCollection();
        var builder = services.AddDaprAiConversation();
        Assert.NotNull(builder);
    }

    [Fact]
    public void AddDaprAiConversation_RegistersIHttpClientFactory()
    {
        var services = new ServiceCollection();
        services.AddDaprAiConversation();
        var serviceProvider = services.BuildServiceProvider();

        var httpClientFactory = serviceProvider.GetService<IHttpClientFactory>();
        Assert.NotNull(httpClientFactory);

        var daprConversationClient = serviceProvider.GetService<DaprConversationClient>();
        Assert.NotNull(daprConversationClient);
    }

    [Fact]
    public void AddDaprAiConversation_NullServices_ShouldThrowException()
    {
        IServiceCollection services = null;
        Assert.Throws<ArgumentNullException>(() => services.AddDaprAiConversation());
    }
}
