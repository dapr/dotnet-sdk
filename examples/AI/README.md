# Dapr AI examples

These examples use the Dapr Conversation API through the `conversation.echo`
component in [`Components\conversation-echo.yaml`](./Components/conversation-echo.yaml).
The echo component makes the examples runnable without credentials for an external
model provider.

| Project | Demonstrates |
|---------|--------------|
| [`ConversationalAI`](./ConversationalAI) | Calling `DaprConversationClient` directly with conversation roles, message content, choices, and tool-call results. |
| [`MicrosoftAiExtensionsDemo`](./MicrosoftAiExtensionsDemo) | Adapting Dapr Conversation to `Microsoft.Extensions.AI.IChatClient`, including an `AIFunction` tool declaration. |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) with an
  [initialized local environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/).

## Run the direct client example

From the repository root:

```powershell
dapr run --app-id conversation-example --resources-path .\examples\AI\Components -- dotnet run --project .\examples\AI\ConversationalAI --framework net10.0
```

The application sends one user message and prints the choices returned by the
echo component.

## Run the Microsoft.Extensions.AI example

From the repository root:

```powershell
dapr run --app-id microsoft-ai-example --resources-path .\examples\AI\Components -- dotnet run --project .\examples\AI\MicrosoftAiExtensionsDemo --framework net10.0
```

The application resolves `IChatClient`, sends a normal chat request, and then
sends a request with a locally declared weather tool. The echo component does not
invoke tools; that call demonstrates registration and request construction only.
