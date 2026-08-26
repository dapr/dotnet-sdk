#nowarn "FS3261"

namespace SecretManagement.FSharp.Sample

open Dapr.SecretsManagement.Abstractions

[<SecretStore("my-vault")>]
type IMyVaultSecrets =
    [<Secret("db-connection-string")>]
    abstract member DatabaseConnection: string with get

    abstract member ApiKey: string with get
