#nowarn "FS3261" "57"
open System
open System.Text
open System.Threading
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Dapr.Cryptography.Encryption
open Dapr.Cryptography.Encryption.Extensions

let componentName = "localstorage"
let keyName = "rsa-private-key.pem"

let builder: WebApplicationBuilder = WebApplication.CreateBuilder()
builder.Services.AddDaprEncryptionClient(Action<IServiceProvider, DaprEncryptionClientBuilder>(fun _ opt ->
    opt.UseHttpEndpoint("http://localhost:6552") |> ignore
    opt.UseGrpcEndpoint("http://localhost:6551") |> ignore
    ())) |> ignore
let app: WebApplication = builder.Build()
let client = app.Services.GetRequiredService<DaprEncryptionClient>()

let plainText = "Hello from F#"
printfn "Original: %s" plainText

let data = Encoding.UTF8.GetBytes(plainText)
let encrypted : ReadOnlyMemory<byte> = client.EncryptAsync(componentName, data, keyName, null, CancellationToken.None).Result
printfn "Encrypted: %A bytes" encrypted.Length

let decrypted : ReadOnlyMemory<byte> = client.DecryptAsync(componentName, encrypted, keyName, null, CancellationToken.None).Result
let decoded = decrypted.ToArray()
printfn "Decrypted: %s" (Encoding.UTF8.GetString(decoded : byte[]))

app.Run()