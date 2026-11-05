# Dapr cryptography example

This console example uses `DaprEncryptionClient` with Dapr's local-storage
cryptography component. It demonstrates:

- Encrypting and decrypting an in-memory UTF-8 string.
- Streaming a file through the encryption and decryption APIs.
- Streaming a larger generated file without buffering it in memory.

The component reads the demonstration RSA private key from `keys\rsa-private-key.pem`.
That checked-in key is public and must never be used outside this example.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0).
- [Dapr CLI](https://docs.dapr.io/getting-started/install-dapr-cli/) with an
  [initialized local environment](https://docs.dapr.io/getting-started/install-dapr-selfhost/).
- A Dapr runtime that includes the cryptography building block.

## Run

Run commands from this directory. The application explicitly connects to Dapr on
HTTP port `6552` and gRPC port `6551`, so the sidecar command must use those ports.

```powershell
Set-Location examples\Cryptography
dapr run --resources-path .\Components --app-id cryptography-example --dapr-http-port 6552 --dapr-grpc-port 6551 -- dotnet run --framework net10.0 -- 0
```

Replace the final argument to select an example:

| Argument | Example |
|----------|---------|
| `0` | Encrypt and decrypt a string |
| `1` | Encrypt and decrypt `file.txt` through streams |
| `2` | Encrypt and decrypt a generated large file through streams |

The application requires a sample number; running without one does not execute
an example.

## Code guide

- [`Program.cs`](./Program.cs) configures `DaprEncryptionClient` and selects the example.
- [`EncryptDecryptStringExample.cs`](./Examples/EncryptDecryptStringExample.cs)
  uses the byte-oriented APIs.
- [`EncryptDecryptFileStreamExample.cs`](./Examples/EncryptDecryptFileStreamExample.cs)
  encrypts a file stream and decrypts the result into a temporary file.
- [`EncryptDecryptLargeFileExample.cs`](./Examples/EncryptDecryptLargeFileExample.cs)
  keeps both directions streaming for a larger payload.
- [`local-storage.yaml`](./Components/local-storage.yaml) configures
  `crypto.dapr.localstorage` to read keys from the local `keys` directory.

## Production considerations

The local-storage component and checked-in private key are for development only.
Production applications should use a supported key-management component, protect
its credentials through the platform's identity facilities, and use independently
managed production keys.
