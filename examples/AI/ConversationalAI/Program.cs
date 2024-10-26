using Dapr.AI.Conversation;
using Dapr.AI.Conversation.Extensions;
using Dapr.AI.Conversation.Models.Request;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprAiConversation();

var app = builder.Build();

var conversationClient = app.Services.GetRequiredService<DaprConversationClient>();
var response = await conversationClient.ConverseAsync("replace-with-component-name",
    new List<DaprLlmInput> { new DaprLlmInput("Hello - anyone out there?") });

Console.WriteLine("Received the following from the LLM:");
foreach (var resp in response.Outputs)
{
    Console.WriteLine($"\t{resp.Result}");
}
