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

var response = await chatClient.GetResponseAsync([
    new ChatMessage(ChatRole.User,
        "Please write me a poem in iambic pentameter about the joys of using Dapr to develop distributed applications with .NET")
]);

foreach (var r in response.Messages)
{
    Console.WriteLine(r.Text);
}
