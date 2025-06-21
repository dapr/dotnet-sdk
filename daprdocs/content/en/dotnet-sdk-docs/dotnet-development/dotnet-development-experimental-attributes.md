---
type: docs
title: "Dapr .NET SDK Development with Dapr CLI"
linkTitle: "Experimental Attributes"
weight: 61000
description: Learn about local development with the Dapr CLI
---

## Experimental Attributes

### Introduction to Experimental Attributes

With the release of .NET 8, C# 12 introduced the `[Experimental]` attribute, which provides a standardized way to mark 
APIs that are still in development or experimental. This attribute is defined in the `System.Diagnostics.CodeAnalysis` 
namespace and requires a diagnostic ID parameter used to generate compiler warnings when the experimental API 
is used.

In the Dapr .NET SDK, we now use the `[Experimental]` attribute instead of `[Obsolete]` to mark building blocks and 
components that have not yet passed the stable lifecycle certification. This approach provides a clearer distinction 
between:

1. **Experimental APIs** - Features that are available but still evolving and have not yet been certified as stable 
2. according to the [Dapr Component Certification Lifecycle](https://docs.dapr.io/operations/components/certification-lifecycle/).

2. **Obsolete APIs** - Features that are truly deprecated and will be removed in a future release.

### Usage in the Dapr .NET SDK

In the Dapr .NET SDK, we apply the `[Experimental]` attribute at the class level for building blocks that are still in 
the Alpha or Beta stages of the [Component Certification Lifecycle](https://docs.dapr.io/operations/components/certification-lifecycle/). 
The attribute includes:

- A diagnostic ID that identifies the experimental building block
- A URL that points to the relevant documentation for that block

For example:

```csharp
csharp using System.Diagnostics.CodeAnalysis;
namespace Dapr.Cryptography.Encryption 
{ 
    [Experimental("DAPR_CRYPTOGRAPHY", UrlFormat = "https://docs.dapr.io/developing-applications/building-blocks/cryptography/cryptography-overview/")] 
    public class DaprEncryptionClient 
    { 
        // Implementation 
    } 
}
```

The diagnostic IDs follow a naming convention of `DAPR_[BUILDING_BLOCK_NAME]`, such as:

- `DAPR_CONVERSATION` - For the Conversation building block
- `DAPR_CRYPTOGRAPHY` - For the Cryptography building block
- `DAPR_JOBS` - For the Jobs building block
- `DAPR_DISTRIBUTEDLOCK` - For the Distributed Lock building block

### Suppressing Experimental Warnings

When you use APIs marked with the `[Experimental]` attribute, the compiler will generate errors. 
To build your solution without marking your own code as experimental, you will need to suppress these errors. Here are 
several approaches to do this:

#### Option 1: Using #pragma directive

You can use the `#pragma warning` directive to suppress the warning for specific sections of code:

```csharp
// Disable experimental warning 
#pragma warning disable DAPR_CRYPTOGRAPHY 
// Your code using the experimental API 
var client = new DaprEncryptionClient(); 
// Re-enable the warning 
#pragma warning restore DAPR_CRYPTOGRAPHY
```

This approach is useful when you want to suppress warnings only for specific sections of your code.

#### Option 2: Using SuppressMessage attribute

For a more targeted approach, you can use the `[SuppressMessage]` attribute:

```csharp
using System.Diagnostics.CodeAnalysis;

[SuppressMessage("Experimental", "DAPR_CRYPTOGRAPHY:Type is experimental", Justification = "Intentionally using experimental Cryptography API")] 
public void MyMethod() 
{ 
    var client = new DaprEncryptionClient(); 
    // Your code 
}
```

This approach is more declarative and provides documentation about why you're suppressing the warning.

#### Option 3: Project-level suppression

To suppress warnings for an entire project, add the following to your `.csproj` file.
file.

```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);DAPR_CRYPTOGRAPHY</NoWarn>
</PropertyGroup>
```

You can include multiple diagnostic IDs separated by semicolons:

```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);DAPR_CONVERSATION;DAPR_JOBS;DAPR_DISTRIBUTEDLOCK;DAPR_CRYPTOGRAPHY</NoWarn>
</PropertyGroup>
```

This approach is particularly useful for test projects that need to use experimental APIs.

#### Option 4: Directory-level suppression

For suppressing warnings across multiple projects in a directory, add a `Directory.Build.props` file:

```xml
<PropertyGroup>
    <NoWarn>$(NoWarn);DAPR_CONVERSATION;DAPR_JOBS;DAPR_DISTRIBUTEDLOCK;DAPR_CRYPTOGRAPHY</NoWarn>
</PropertyGroup>
```

This file should be placed in the root directory of your test projects. You can learn more about using 
`Directory.Build.props` files in the 
[MSBuild documentation](https://learn.microsoft.com/en-us/visualstudio/msbuild/customize-by-directory).

### Lifecycle of Experimental APIs

As building blocks move through the certification lifecycle and reach the "Stable" stage, the `[Experimental]` attribute will be removed. No migration or code changes will be required from users when this happens, except for the removal of any warning suppressions if they were added.

Conversely, the `[Obsolete]` attribute will now be reserved exclusively for APIs that are truly deprecated and scheduled for removal. When you see a method or class marked with `[Obsolete]`, you should plan to migrate away from it according to the migration guidance provided in the attribute message.

### Best Practices

1. **In application code:**
    - Be cautious when using experimental APIs, as they may change in future releases
    - Consider isolating usage of experimental APIs to make future updates easier
    - Document your use of experimental APIs for team awareness

2. **In test code:**
    - Use project-level suppression to avoid cluttering test code with warning suppressions
    - Regularly review which experimental APIs you're using and check if they've been stabilized

3. **When contributing to the SDK:**
    - Use `[Experimental]` for new building blocks that haven't completed certification
    - Use `[Obsolete]` only for truly deprecated APIs
    - Provide clear documentation links in the `UrlFormat` parameter

### Additional Resources

- [Dapr Component Certification Lifecycle](https://docs.dapr.io/operations/components/certification-lifecycle/)
- [C# Experimental Attribute Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-12.0/experimental-attribute)
