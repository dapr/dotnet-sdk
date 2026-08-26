#nowarn "FS3261"

namespace Dapr.IntegrationTest.FSharp.SecretsManagement

open Dapr.SecretsManagement.Abstractions

[<SecretStore("localsecretstore")>]
type ILocalSecrets =
    [<Secret("secret1")>]
    abstract member Secret1: string with get

    abstract member Secret2: string with get
