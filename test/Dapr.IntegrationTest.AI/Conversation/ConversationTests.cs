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

using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;
using Dapr.Testcontainers;
using Dapr.Testcontainers.Common;
using Dapr.Testcontainers.Harnesses;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dapr.IntegrationTest.AI.Conversation;

public sealed class ConversationTests
{
    [Fact]
    public async Task ShouldProcessConversation()
    {
        var componentsDir = TestDirectoryManager.CreateTestDirectory("conversation-components");

        await using var environment = await DaprTestEnvironment.CreateWithPooledNetworkAsync();
        await environment.StartAsync();

        var harness = new DaprHarnessBuilder(componentsDir)
            .WithEnvironment(environment)
            .BuildConversation();
        await using var testApp = await DaprHarnessBuilder.ForHarness(harness)
            .ConfigureServices(builder =>
            {
                builder.Services.AddDaprConversationClient((sp, clientBuilder) =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var grpcEndpoint = config["DAPR_GRPC_ENDPOINT"];
                    var httpEndpoint = config["DAPR_HTTP_ENDPOINT"];

                    if (!string.IsNullOrEmpty(grpcEndpoint))
                        clientBuilder.UseGrpcEndpoint(grpcEndpoint);
                    if (!string.IsNullOrEmpty(httpEndpoint))
                        clientBuilder.UseHttpEndpoint(httpEndpoint);
                });
            })
            .BuildAndStartAsync();

        using var scope = testApp.CreateScope();
        var client = scope.ServiceProvider.GetRequiredService<DaprConversationClient>();

        var inputs = new[]
        {
            new ConversationInput(
            [
                new SystemMessage
                    {
                        Content = [new MessageContent("You are a concise assistant.")]
                    },
                    new UserMessage
                    {
                        Content = [new MessageContent("Respond with a short greeting.")]
                    }
            ])
        };

        var options = new ConversationOptions(Constants.DaprComponentNames.ConversationComponentName)
        {
            Temperature = 0
        };

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var response = await client.ConverseAsync(inputs, options, cts.Token);

        Assert.NotNull(response);
        Assert.Single(response.Outputs);
        Assert.NotEmpty(response.Outputs[0].Choices);
        Assert.False(string.IsNullOrWhiteSpace(response.Outputs[0].Choices[0].Message.Content));
    }
}
