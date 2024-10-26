using System;
using System.Net.Http;
using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.AI.Test.Conversation.Extensions;

public class DaprAiConversationBuilderExtensionsTest
{
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
