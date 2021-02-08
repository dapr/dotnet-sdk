---
type: docs
title: "Working with cancellation tokens"
linkTitle: "Cancellation tokens"
weight: 100000
description: How to use cancellation tokens in the .NET SDK
---

Asynchronous APIs exposed by `DaprClient` accept a cancellation token and by default, if the operation is canceled, you will get an OperationCanceledException. However, if you choose to initialize and pass in your own GrpcChannelOptions to the client builder, then unless you enable the [ThrowOperationCanceledOnCancellation setting](https://grpc.github.io/grpc/csharp-dotnet/api/Grpc.Net.Client.GrpcChannelOptions.html#Grpc_Net_Client_GrpcChannelOptions_ThrowOperationCanceledOnCancellation), the exception thrown would be an RpcException with StatusCode as Cancelled. To get an OperationCanceledException instead, refer to the code below:-
```c#
var httpClient = new HttpClient();
var daprClient = new DaprClientBuilder()
    .UseGrpcChannelOptions(new GrpcChannelOptions { HttpClient = httpClient, ThrowOperationCanceledOnCancellation = true })
    .Build();
```