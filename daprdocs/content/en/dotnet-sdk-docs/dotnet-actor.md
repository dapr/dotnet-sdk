---
type: docs
title: "Getting started with the Dapr actor .NET SDK"
linkTitle: "Actor"
weight: 20000
description: How to get up and running with the Dapr .NET SDK
---

The Dapr actor package allows you to interact with Dapr virtual actors from a Python application.

## Pre-requisites

- [Dapr CLI]({{< ref install-dapr-cli.md >}}) installed
- Initialized [Dapr environment]({{< ref install-dapr-selfhost.md >}})
- [Dotnet 5.0+](https://dotnet.microsoft.com/download) installed

## Actor interface

The interface defines the actor contract that is shared between the actor implementation and the clients calling the actor. Because a client may depend on it, it typically makes sense to define it in an assembly that is separate from the actor implementation.

## Actor services

An actor service hosts the virtual actor. It is implemented a class that derives from the base type `Actor` and implements the interfaces defined in the actor interface.

## Actor client

An actor client contains the implementation of the actor client which calls the actor methods defined in the actor interface.

## Sample

Visit [this page](https://github.com/dapr/dotnet-sdk/tree/master/samples/Actor) for a runnable actor sample.