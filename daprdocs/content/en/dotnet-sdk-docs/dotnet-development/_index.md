---
type: docs
title: "Developing applications with the Dapr .NET SDK"
linkTitle: "Dev integrations"
weight: 50000
description: Learn about local development integration options for .NET Dapr applications
---

## Thinking more than one at a time

Using your favorite IDE or editor to launch an application typically assumes that you only need to run one thing: the application you're debugging. However, developing microservices challenges you to think about your local development process for *more than one at a time*. A microservices application has multiple services that you might need running simultaneously, and dependencies (like state stores) to manage.

Adding Dapr to your development process means you need to manage the following concerns:

- Each service you want to run
- A Dapr sidecar for each service
- Dapr component and configuration manifests 
- Additional dependencies such as state stores
- optional: the Dapr placement service for actors

This document assumes that you're building a production application, and want to create a repeatable and robust set of development practices. The guidance here is general, and applies to any .NET server application using Dapr (including actors).

## Managing components

You have two primary methods of storing component definitions for local development with Dapr:

- Use the default location (`~/.dapr/components`)
- Use your own location 

Creating a folder within your source code repository to store components and configuration will give you a way to version and share these definitions. The guidance provided here will assume you created a folder next to the application source code to store these files.

## Development options

Choose one of these links to learn about tools you can use in local development scenarios. These articles are ordered from lowest investment to highest investment. You may want to read them all to get an overview of your options.
