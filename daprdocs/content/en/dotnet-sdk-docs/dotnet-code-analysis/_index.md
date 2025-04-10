---
type: docs
title: "Overview of Dapr source code analysis"
linkTitle: "Code Analysis"
weight: 70000
description: Code analyzers and fixes for common Dapr issues
no_list: true
---

Dapr supports a growing collection of optional Roslyn analyzers and code fix providers that inspect your code for
code quality issues. Starting with the release of v1.16, developers have the opportunity to install additional projects
from NuGet alongside each of the standard capability packages to enable these analyzers in their solutions.

{{% alert title="Note" color="primary" %}}

A future release of the Dapr .NET SDK will include these analyzers by default without necessitating a separate package
install.

{{% /alert %}}

Rule violations will typically be marked as `Info` or `Warning` so that if the analyzer identifies an issue, it won't
necessarily break builds. All code analysis violations appear with the prefix "DAPR" and are uniquely distinguished
by a number following this prefix. 

{{% alert title="Note" color="primary" %}}

At this time, the first two digits of the diagnostic identifier map one-to-one to distinct Dapr packages, but this 
is subject to change in the future as more analyzers are developed.

{{% /alert %}}

## Install and configure analyzers
The following packages will be available via NuGet following the v1.16 Dapr release:
- Dapr.Actors.Analyzers
- Dapr.Jobs.Analyzers
- Dapr.PubSub.Analyzers
- Dapr.Workflow.Analyzers

Install each NuGet package on every project where you want the analyzers to run. The package will be installed as a
project dependency and analyzers will run as you write your code or as part of a CI/CD build. The analyzers will flag
issues in your existing code and warn you about new issues as you build your project.

Many of our analyzers have associated code fixes that can be applied to automatically correct the problem. If your IDE
supports this capability, any available code fixes will show up as an inline menu option in your code.

Further, most of our analyzers should also report a specific line and column number in your code of the syntax that's 
been identified as a key aspect of the rule. If your IDE supports it, double clicking any of the analyzer warnings 
should jump directly to the part of your code responsible for the violating the analyzer's rule.

### Suppress specific analyzers
If you wish to keep an analyzer from firing against some particular piece of your project, their outputs can be
individually targeted for suppression through a number of ways. Read more about suppressing analyzers in projects
or files in the associated [.NET documentation](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/suppress-warnings#use-the-suppressmessageattribute).

### Disable all analyzers
If you wish to disable all analyzers in your project without removing any packages providing them, set
the `EnableNETAnalyzers` property to `false` in your csproj file.

## Available Analyzers

| Diagnostic ID | Dapr Package | Category         | Severity     | Version Added                                                                                                                     | Description                                                                         | Code Fix Available |
| -- | -- |------------------|--------------|-----------------------------------------------------------------------------------------------------------------------------------|-------------------------------------------------------------------------------------| -- |
| DAPR1201 | Dapr.PubSub | Usage | Warning | 1.16 | Call app.MapSubscribeHandler to map endpoint for Dapr PubSub subscriptions          | Yes |
| DAPR1301 | Dapr.Workflow | Usage | Warning      | 1.16                                                                                                                              | The workflow type is not registered with the dependency injection provider          | Yes |
| DAPR1302 | Dapr.Workflow | Usage | Warning | 1.16                                                                                                                              | The workflow activity type is not registered with the dependency injection provider | Yes | 
| DAPR1401 | Dapr.Actors | Usage            | Warning      | 1.16                                                                                                                              | Actor timer method invocations require the named callback method to exist on type   | No                 |
| DAPR1402 | Dapr.Actors | Usage            | Warning      | The actor type is not registered with dependency injection                                                                        | Yes                                                                                 |
| DAPR1403 | Dapr.Actors | Interoperability | Info         | Set options.UseJsonSerialization to true to support interoperability with non-.NET actors                                         | Yes                                                                                 |
| DAPR1404 | Dapr.Actors | Usage            | Warning      | Call app.MapActorsHandlers to map endpoints for Dapr actors                                                                       | Yes                                                                                 |
| DAPR1501 | Dapr.Jobs | Usage            | Warning      | Job invocations require the MapDaprScheduledJobHandler to be set and configured for each anticipated job on IEndpointRouteBuilder | No                                                                                  |

## Analyzer Categories
The following are each of the eligible categories that an analyzer can be assigned to and are modeled after the 
standard categories used by the.NET analyzers:
- Design
- Documentation
- Globalization
- Interoperability
- Maintainability
- Naming
- Performance
- Reliability
- Security
- Usage