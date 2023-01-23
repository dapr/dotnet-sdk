# Dapr Workflow with ASP.NET Core sample

This Dapr workflow example shows how to create a Dapr workflow (`Workflow`) and invoke it using ASP.NET Core web APIs.

## Prerequisites

- [.NET 6+](https://dotnet.microsoft.com/download) installed
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/)
- [Initialized Dapr environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/)
- [Dapr Workflow .NET SDK](https://github.com/dapr/dotnet-sdk/)

## Projects in sample

This sample contains a single [WorkflowWebApp](./WorkflowWebApp) ASP.NET Core project. It combines both the web APIs for managing the workflows and the workflows themselves.

