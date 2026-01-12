using Dapr.AI.Conversation;
using Dapr.AI.Conversation.ConversationRoles;
using Dapr.AI.Conversation.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDaprConversationClient();

var app = builder.Build();

var conversationClient = app.Services.GetRequiredService<DaprConversationClient>();

// Send a message to the conversation service
var response = await conversationClient.ConverseAsync(
    [
        new ConversationInput(
            new List<IConversationMessage>
            {
                new UserMessage
                {
                    Name = "Test User",
                    Content =
                    [
                        new MessageContent(
                            "Please write a witty haiku about the Dapr distributed programming framework at dapr.io")
                    ]
                }
            }
        )
    ],
    new ConversationOptions("conversation")
);

Console.WriteLine("Received the following from the LLM:");
foreach (var resp in response.Outputs)
{
    foreach (var choice in resp.Choices)
    {
        Console.WriteLine($"{choice.Index} - Reason: {choice.FinishReason}");
        Console.WriteLine($"\tMesage: '{choice.Message.Content}'");
        Console.WriteLine("\tTools:");
        foreach (var tool in choice.Message.ToolCalls)
        {
            if (tool is CalledToolFunction calledToolFunction)
            {
                Console.WriteLine($"\t\tId: {calledToolFunction.Id}, Name: {calledToolFunction.Name}, Arguments: {calledToolFunction.JsonArguments}");
            }
            else
            {
                Console.WriteLine($"\t\tId: {tool.Id}");
            }
        }   
    }
}
