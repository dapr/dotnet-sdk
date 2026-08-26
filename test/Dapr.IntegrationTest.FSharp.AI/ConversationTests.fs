#nowarn "FS3261"
#nowarn "57"

namespace Dapr.IntegrationTest.FSharp.AI

open System
open System.Threading
open Dapr.AI.Conversation
open Dapr.AI.Conversation.ConversationRoles
open Dapr.AI.Conversation.Extensions
open Dapr.AI.Conversation.Tools
open Dapr.Testcontainers
open Dapr.Testcontainers.Common
open Dapr.Testcontainers.Harnesses
open global.Dapr.Testcontainers.Xunit.Attributes
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open global.Xunit

type ConversationTests() =
    [<MinimumDaprRuntimeFact("1.17")>]
    member _.ShouldProcessConversation() = task {
        let componentsDir = TestDirectoryManager.CreateTestDirectory("fsharp-conversation-components")

        use! env = DaprTestEnvironment.CreateWithPooledNetworkAsync(cancellationToken = TestContext.Current.CancellationToken)
        do! env.StartAsync(TestContext.Current.CancellationToken)

        let harness = DaprHarnessBuilder(componentsDir).WithEnvironment(env).BuildConversation()
        use! testApp =
            DaprHarnessBuilder.ForHarness(harness)
                .ConfigureServices(fun builder ->
                    builder.Services.AddDaprConversationClient(
                        Action<IServiceProvider, DaprConversationClientBuilder>(
                            fun sp clientBuilder ->
                                let config = sp.GetRequiredService<IConfiguration>()
                                let grpcEndpoint = config["DAPR_GRPC_ENDPOINT"]
                                let httpEndpoint = config["DAPR_HTTP_ENDPOINT"]
                                if not (String.IsNullOrEmpty(grpcEndpoint)) then
                                    clientBuilder.UseGrpcEndpoint(grpcEndpoint) |> ignore
                                if not (String.IsNullOrEmpty(httpEndpoint)) then
                                    clientBuilder.UseHttpEndpoint(httpEndpoint) |> ignore))
                    |> ignore)
                .BuildAndStartAsync()

        use scope = testApp.CreateScope()
        let client = scope.ServiceProvider.GetRequiredService<DaprConversationClient>()

        let inputs =
            [|
                ConversationInput(
                    [|
                        SystemMessage(
                            Content = [| MessageContent("You are a concise assistant.") |]
                        )
                        UserMessage(
                            Content = [| MessageContent("Respond with a short greeting.") |]
                        )
                    |])
            |]

        let options =
            ConversationOptions(Constants.DaprComponentNames.ConversationComponentName,
                Temperature = Nullable(0.0))

        use cts = new CancellationTokenSource(TimeSpan.FromSeconds(60.0))
        let! response = client.ConverseAsync(inputs, options, cts.Token)

        Assert.NotNull(response)
        Assert.Single(response.Outputs) |> ignore
        Assert.NotEmpty(response.Outputs[0].Choices)
        Assert.False(String.IsNullOrWhiteSpace(response.Outputs[0].Choices[0].Message.Content))
    }
