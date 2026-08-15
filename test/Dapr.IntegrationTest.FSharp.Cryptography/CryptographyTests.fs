#nowarn "FS3261"
#nowarn "57"

namespace Dapr.IntegrationTest.FSharp.Cryptography

open System
open System.IO
open System.Text
open Dapr.Cryptography.Encryption
open Dapr.Cryptography.Encryption.Extensions
open Dapr.Cryptography.Encryption.Models
open Dapr.Testcontainers
open Dapr.Testcontainers.Common
open Dapr.Testcontainers.Harnesses
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.DependencyInjection
open Xunit

type CryptographyTests() =
    [<Fact>]
    member _.ShouldEncryptAndDecryptString() = task {
        let componentsDir = TestDirectoryManager.CreateTestDirectory("fsharp-crypto-components")
        let keysDir = Path.Combine(componentsDir, "keys")
        Directory.CreateDirectory(keysDir) |> ignore
        let sourceKey = Path.Combine(AppContext.BaseDirectory, "keys", "rsa-private-key.pem")
        File.Copy(sourceKey, Path.Combine(keysDir, "rsa-private-key.pem"))
        let containerKeyPath = "/components/keys"

        let plaintext = "The quick brown fox jumps over the lazy dog"

        use! env = DaprTestEnvironment.CreateWithPooledNetworkAsync(cancellationToken = TestContext.Current.CancellationToken)
        do! env.StartAsync(TestContext.Current.CancellationToken)

        let harness = DaprHarnessBuilder(componentsDir).BuildCryptography(containerKeyPath)
        use! testApp =
            DaprHarnessBuilder.ForHarness(harness)
                .ConfigureServices(fun builder ->
                    builder.Services.AddDaprEncryptionClient(
                        Action<IServiceProvider, DaprEncryptionClientBuilder>(
                            fun sp clientBuilder ->
                                let config = sp.GetRequiredService<IConfiguration>()
                                let grpcEndpoint = config["DAPR_GRPC_ENDPOINT"]
                                if not (String.IsNullOrEmpty(grpcEndpoint)) then
                                    clientBuilder.UseGrpcEndpoint(grpcEndpoint) |> ignore))
                    |> ignore)
                .BuildAndStartAsync()

        use scope = testApp.CreateScope()
        let client = scope.ServiceProvider.GetRequiredService<DaprEncryptionClient>()

        let plaintextBytes = ReadOnlyMemory<byte>(Encoding.UTF8.GetBytes(plaintext))
        let! encrypted =
            client.EncryptAsync(
                Constants.DaprComponentNames.CryptographyComponentName,
                plaintextBytes,
                "rsa-private-key.pem",
                EncryptionOptions(KeyWrapAlgorithm.Rsa),
                TestContext.Current.CancellationToken
            )
        Assert.False(encrypted.IsEmpty)

        let! decrypted =
            client.DecryptAsync(
                Constants.DaprComponentNames.CryptographyComponentName,
                encrypted,
                "rsa-private-key.pem",
                cancellationToken = TestContext.Current.CancellationToken
            )
        Assert.Equal(plaintext, Encoding.UTF8.GetString(decrypted.Span))
    }
