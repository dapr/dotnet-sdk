using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Microsoft.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices(services =>
{
    services.AddDaprConversationClient();
    services.AddDaprChatClient(opt =>
    {
        opt.ConversationComponentName = "conversation";
    });
});

var app = builder.Build();

using var scope = app.Services.CreateScope();
var chatClient = scope.ServiceProvider.GetRequiredService<IChatClient>();

// Vanilla message/response
var response = await chatClient.GetResponseAsync([
    new ChatMessage(ChatRole.User,
        "Please write me a poem in iambic pentameter about the joys of using Dapr to develop distributed applications with .NET")
]);

foreach (var r in response.Messages)
{
    Console.WriteLine(r.Text);
}

// Tool-based support - This just demonstrates how to do it, but Dapr's echo conversation component doesn't use tools
string GetCurrentWeather() => Random.Shared.NextDouble() > 0.5 ? "It's sunny today!" : "It's raining today!";
var toolChatOptions = new ChatOptions { Tools = [AIFunctionFactory.Create(GetCurrentWeather, "weather")] };
var toolResponse = await chatClient.GetResponseAsync("What's the weather like today?", toolChatOptions);
foreach (var toolResp in toolResponse.Messages)
{
    Console.WriteLine(toolResp);
}
