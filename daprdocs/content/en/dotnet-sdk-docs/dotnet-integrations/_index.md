---
type: docs
title: "Developing applications with the Dapr .NET SDK"
linkTitle: "Deployment Integrations"
weight: 90000
description: Deployment integrations with the Dapr .NET SDK
---

## Thinking more than one at a time

Using your favorite IDE or editor to launch an application typically assumes that you only need to run one thing: 
the application you're debugging. However, developing microservices challenges you to think about your local 
development process for *more than one at a time*. A microservices application has multiple services that you might 
need running simultaneously, and dependencies (like state stores) to manage.

Adding Dapr to your development process means you need to manage the following concerns:

- Each service you want to run
- A Dapr sidecar for each service
- Dapr component and configuration manifests 
- Additional dependencies such as state stores
- optional: the Dapr placement service for actors

This document assumes that you're building a production application and want to create a repeatable and robust set of 
development practices. The guidance here is generalized, and applies to any .NET server application using 
Dapr (including actors).

## Managing components

You have two primary methods of storing component definitions for local development with Dapr:

- Use the default location (`~/.dapr/components`)
- Use your own location 

Creating a folder within your source code repository to store components and configuration will give you a way to 
version and share these definitions. The guidance provided here will assume you created a folder next to the 
application source code to store these files.

## Development options

Choose one of these links to learn about tools you can use in local development scenarios. It's suggested that 
you familiarize yourself with each of them to get a sense of the options provided by the .NET SDK.
