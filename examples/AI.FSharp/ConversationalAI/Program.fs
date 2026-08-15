#nowarn "FS3261" "57"
open System
open System.Collections.Generic
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Dapr.AI.Conversation
open Dapr.AI.Conversation.ConversationRoles
open Dapr.AI.Conversation.Extensions

let builder: WebApplicationBuilder = WebApplication.CreateBuilder()
builder.Services.AddDaprConversationClient() |> ignore
let app: WebApplication = builder.Build()

let client = app.Services.GetRequiredService<DaprConversationClient>()

let messages = ResizeArray<IConversationMessage>()
let msg = UserMessage(Name = "Test User")
msg.Content <- ResizeArray<MessageContent>([ MessageContent("Write a haiku about Dapr") ])
messages.Add(msg)

let input = ConversationInput(messages)

let options = ConversationOptions("conversation")
let response = client.ConverseAsync([| input |], options).Result

printfn "Received response from LLM:"
for resp in response.Outputs do
    for choice in resp.Choices do
        printfn "%A - %A" choice.Index choice.FinishReason
        printfn "  Message: %A" choice.Message.Content

app.Run()