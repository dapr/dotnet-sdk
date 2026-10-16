# Binary Store Example

This console example follows the same dependency-injection and source-generator
patterns as `StateManagementExample`, using the Dapr `binarystore.in-memory`
component. No external storage service is required, but a Dapr sidecar is.
Files live in the sidecar's memory and are lost when it stops.

## What it demonstrates

- Registering the SDK with `AddDaprBinaryStoreClient()`.
- Generating a typed `IDocumentStore` client with `[BinaryStore("binarystore")]`
  and registering it with `WithDocumentStore()`.
- Uploading, downloading, overwriting, and deleting an in-memory document.
- Using `DaprBinaryStoreClient` directly to upload a multi-chunk binary file,
  download it to a stream, and verify its contents.
- Reading a file as an asynchronous sequence of chunks.

## Component size limits

The component in `components\binarystore.yaml` configures two independent,
optional limits. Each accepts either a quoted byte count, such as `"1048576"`,
or a byte-size quantity string, such as `"1Ki"`, `"50Mi"`, or `"1Gi"`.
For example, `"1Mi"` means 1 MiB (1,048,576 bytes). Values must be greater than zero.

| Parameter | Scope | Sample configuration | Default when omitted |
|-----------|-------|----------------------|----------------------|
| `maxSize` | Maximum size of a single file, regardless of upload chunk size. | `"1Mi"` (1 MiB) | `"4Mi"` (4 MiB) |
| `maxTotalSize` | Maximum combined size of stored files and in-flight upload reservations in this component instance. | `"25Mi"` (25 MiB) | `"512Mi"` (512 MiB) |

Either parameter can be omitted independently; omitting one uses its default,
not an unlimited size. See the component's
[metadata reference](https://github.com/dapr/components-contrib/blob/main/binarystore/in-memory/metadata.yaml)
for supported values and defaults.

Both effective limits must be satisfied. With the sample configuration, a 2 MiB
file exceeds `maxSize` even when the store is empty. Twenty-five 1 MiB files fill
the store, so another upload would exceed `maxTotalSize`. Upload capacity is
reserved as data is read. During an overwrite, both the old file and the incoming
replacement count toward the total until the upload commits, so replacing a file
requires free capacity for the new content.

Deleting a file frees capacity for subsequent uploads. The sample deletes its
files before exiting.

## Prerequisites

- .NET 8, 9, or 10 SDK.
- Dapr CLI and an initialized self-hosted Dapr runtime **1.19 or later**. For
  prerelease runtimes, use `1.19.0-rc.3` or later; earlier release candidates may
  not include the Binary Store API or its in-memory component.

For a new local installation, initialize a compatible runtime:

```powershell
dapr init --runtime-version 1.19.0-rc.3
```

## Run

From the repository root:

```powershell
Set-Location examples\BinaryStore
dapr run --app-id binarystore-example --dapr-grpc-port 50001 --resources-path .\components -- dotnet run --project .\BinaryStoreExample --framework net10.0
```

Use `net8.0` or `net9.0` instead if that is your installed SDK. The application is
a console client, so no application port or HTTP server is needed. Errors are
allowed to propagate rather than being reported as successful operations.

The output shows the typed client's document operations, followed by the direct
client uploading and verifying a 98,321-byte binary file. Upload chunk size is
chosen by the SDK caller; download chunk sizes are chosen by the Dapr runtime.

## Using NuGet

The example references the SDK projects so it can run from this repository.
In your own application, install the single `Dapr.BinaryStore` package instead.
It bundles the abstractions, runtime implementation, and source generator; these
are not published as separate packages.

```powershell
dotnet add package Dapr.BinaryStore --prerelease
```
