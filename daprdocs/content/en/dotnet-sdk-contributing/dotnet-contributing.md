---
type: docs
title: "Contributing to the .NET SDK"
linkTitle: ".NET SDK"
weight: 3000
description: Guidelines for contributing to the Dapr .NET SDK
---

# Welcome!
If you're reading this, you're likely interested in contributing to Dapr and/or the Dapr .NET SDK. Welcome to the project
and thank you for your interest in contributing!

Please review the documentation, familiarize yourself with what Dapr is and what it's seeking to accomplish and reach
out on [Discord](https://bit.ly/dapr-discord). Let us know how you'd like to contribute and we'd be happy to chime in
with ideas and suggestions.

There are many ways to contribute to Dapr:
- Submit bug reports for the [Dapr runtime](https://github.com/dapr/dapr/issues/new/choose) or the [Dapr .NET SDK](https://github.com/dapr/dotnet-sdk/issues/new/choose)
- Propose new [runtime capabilities](https://github.com/dapr/proposals/issues/new/choose) or [SDK functionality](https://github.com/dapr/dotnet-sdk/issues/new/choose)
- Improve the documentation in either the [larger Dapr project](https://github.com/dapr/docs) or the [Dapr .NET SDK specifically](https://github.com/dapr/dotnet-sdk/tree/master/daprdocs) 
- Add new or improve existing [components](https://github.com/dapr/components-contrib/) that implement the various building blocks
- Augment the [.NET pluggable component SDK capabilities](https://github.com/dapr-sandbox/components-dotnet-sdk)
- Improve the Dapr .NET SDK code base and/or fix a bug (detailed below)

If you're new to the code base, please feel encouraged to ask in the #dotnet-sdk channel in Discord about how
to implement changes or generally ask questions. You are not required to seek permission to work on anything, but do
note that if an issue is assigned to someone, it's an indication that someone might have already started work on it.
Especially if it's been a while since the last activity on that issue, please feel free to reach out and see if it's 
still something they're interested in pursuing or whether you can take over, and open a pull request with your 
implementation.

If you'd like to assign yourself to an issue, respond to the conversation with "/assign" and the bot will assign you
to it.

We have labeled some issues as `good-first-issue` or `help wanted` indicating that these are likely to be small,
self-contained changes.

If you're not certain about your implementation, please create it as a draft pull request and solicit feedback
from the [.NET maintainers](https://github.com/orgs/dapr/teams/maintainers-dotnet-sdk) by tagging 
`@dapr/maintainers-dotnet-sdk` and providing some context about what you need assistance with. 

# Contribution Rules and Best Practices

When contributing to the [.NET SDK](https://github.com/dapr/dotnet-sdk) the following rules and best-practices should 
be followed.

## Pull Requests
Pull requests that contain only formatting changes are generally discouraged. Pull requests should instead seek to 
fix a bug, add new functionality, or improve on existing capabilities.

Do aim to minimize the contents of your pull request to span only a single issue. Broad PRs that touch on a lot of files
are not likely to be reviewed or accepted in a short timeframe. Accommodating many different issues in a single PR makes
it hard to determine whether your code fully addresses the underlying issue(s) or not and complicates the code review.

## Tests
All pull requests should include unit and/or integration tests that reflect the nature of what was added or changed
so it's clear that the functionality works as intended. Avoid using auto-generated tests that duplicate testing the
same functionality several times. Rather, seek to improve code coverage by validating each possible path of your 
changes so future contributors can more easily navigate the contours of your logic and more readily identify limitations.

## Examples

The `examples` directory contains code samples for users to run to try out specific functionality of the various 
Dapr .NET SDK packages and extensions. When writing new and updated samples keep in mind:

- All examples should be runnable on Windows, Linux, and MacOS. While .NET Core code is consistent among operating 
systems, any pre/post example commands should provide options through 
[codetabs]({{< ref "contributing-docs.md#tabbed-content" >}})
- Contain steps to download/install any required pre-requisites. Someone coming in with a fresh OS install should be 
able to start on the example and complete it without an error. Links to external download pages are fine.

## Documentation

The `daprdocs` directory contains the markdown files that are rendered into the [Dapr Docs](https://docs.dapr.io) website. When the 
documentation website is built this repo is cloned and configured so that its contents are rendered with the docs 
content. When writing docs keep in mind:

   - All rules in the [docs guide]({{< ref contributing-docs.md >}}) should be followed in addition to these.
   - All files and directories should be prefixed with `dotnet-` to ensure all file/directory names are globally 
   - unique across all Dapr documentation.

All pull requests should strive to include both XML documentation in the code clearly indicating what functionality
does and why it's there as well as changes to the published documentation to clarify for other developers how your change
improves the Dapr framework.

## GitHub Dapr Bot Commands

Checkout the [daprbot documentation](https://docs.dapr.io/contributing/daprbot/) for Github commands you can run in this repo for common tasks. For example, 
you can comment `/assign` on an issue to assign it to yourself.

## Commit Sign-offs
All code submitted to the Dapr .NET SDK must be signed off by the developer authoring it. This means that every
commit must end with the following:
> Signed-off-by: First Last <flast@example.com>

The name and email address must match the registered GitHub name and email address of the user committing the changes.
We use a bot to detect this in pull requests and we will be unable to merge the PR if this check fails to validate.

If you notice that a PR has failed to validate because of a failed DCO check early on in the PR history, please consider
squashing the PR locally and resubmitting to ensure that the sign-off statement is included in the commit history.

# Languages, Tools and Processes
All source code in the Dapr .NET SDK is written in C# and targets the latest language version available to the earliest
supported .NET SDK. As of v1.16, this means that both .NET 8 and .NET 9 are supported. The latest language version available
is [C# version 12](https://learn.microsoft.com/en-us/dotnet/csharp/whats-new/csharp-version-history#c-version-12)

Contributors are welcome to use whatever IDE they're most comfortable developing in, but please do not submit 
IDE-specific preference files along with your contributions as these will be rejected.