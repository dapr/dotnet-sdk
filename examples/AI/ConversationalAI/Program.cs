using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprAiConversation();

var app = builder.Build();

var conversationClient = app.Services.GetRequiredService<DaprConversationClient>();
var response = await conversationClient.ConverseAsync("conversation",
    new List<DaprConversationInput>
    {
        new DaprConversationInput(
            "Please write a witty haiku about the Dapr distributed programming framework at dapr.io",
            DaprConversationRole.Generic)
    });

Console.WriteLine("Received the following from the LLM:");
foreach (var resp in response.Outputs)
{
    Console.WriteLine($"\t{resp.Result}");
}
