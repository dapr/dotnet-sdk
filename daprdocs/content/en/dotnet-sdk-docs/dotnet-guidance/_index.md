---
type: docs
title: "Best Practices for the Dapr .NET SDK"
linkTitle: "Best Practices"
weight: 85000
description: Using Dapr .NET SDK effectively
---

## Building with confidence

The Dapr .NET SDK offers a rich set of capabilities for building distributed applications. This section provides 
practical guidance for using the SDK effectively in production scenarios—focusing on reliability, maintainability, and 
developer experience.

Topics covered include:

- Error handling strategies across Dapr building blocks
- Managing experimental features and suppressing related warnings
- Leveraging source analyzers and generators to reduce boilerplate and catch issues early
- General .NET development practices in Dapr-based applications

## Error model guidance

Dapr operations can fail for many reasons—network issues, misconfigured components, or transient faults. The SDK
provides structured error types to help you distinguish between retryable and fatal errors.

Learn how to use `DaprException` and its derived types effectively [here]({{% ref dotnet-guidance-error-model.md %}}).

## Experimental attributes

Some SDK features are marked as experimental and may change in future releases. These are annotated with 
`[Experimental]` and generate build-time warnings by default. You can:

- Suppress warnings selectively using `#pragma warning disable`
- Use `SuppressMessage` attributes for finer control
- Track experimental usage across your codebase

Learn more about our use of the `[Experimenta]` attribute [here]({{% ref dotnet-guidance-experimental-attributes.md %}}).

## Source tooling

The SDK includes Roslyn-based analyzers and source generators to help you write better code with less effort. These tools:

- Warn about common misuses of the SDK
- Generate boilerplate for actor registration and invocation
- Support IDE integration for faster feedback

Read more about how to install and use these analyzers [here]({{% ref dotnet-guidance-source-generators.md %}}).

## Additional guidance

This section is designed to support a wide range of development scenarios. As your applications grow in complexity, you'll find increasingly relevant practices and patterns for working with Dapr in .NET—from actor lifecycle management to configuration strategies and performance tuning.

