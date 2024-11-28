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

using Dapr.Messaging.PublishSubscribe;
using Dapr.Messaging.PublishSubscribe.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Dapr.Messaging.Test.Extensions;

public class PublishSubscribeServiceCollectionExtensionsTests
{
    [Fact]
    public void AddDaprPubSubClient_RegistersServicesCorrectly()
    {
        var services = new ServiceCollection();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        services.AddSingleton(httpClientFactoryMock.Object);

        services.AddDaprPubSubClient();

        var serviceProvider = services.BuildServiceProvider();
        var daprClient = serviceProvider.GetService<DaprPublishSubscribeClient>();
        Assert.NotNull(daprClient);
    }

    [Fact]
    public void AddDaprPubSubClient_CallsConfigureAction()
    {
        var services = new ServiceCollection();
        var httpClientFactoryMock = new Mock<IHttpClientFactory>();
        services.AddSingleton(httpClientFactoryMock.Object);

        var configureCalled = false;

        services.AddDaprPubSubClient(Configure);

        var serviceProvider = services.BuildServiceProvider();
        var daprClient = serviceProvider.GetService<DaprPublishSubscribeClient>();
        Assert.NotNull(daprClient);
        Assert.True(configureCalled);
        return;

        void Configure(IServiceProvider sp, DaprPublishSubscribeClientBuilder builder)
        {
            configureCalled = true;
        }
    }   
}
