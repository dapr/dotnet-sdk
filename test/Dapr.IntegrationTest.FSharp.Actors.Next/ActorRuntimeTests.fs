#nowarn "FS3261"
namespace Dapr.IntegrationTest.FSharp.Actors.Next

open System
open System.Threading
open System.Threading.Tasks
open Dapr.Actors.Next.Abstractions
open Dapr.Actors.Next.Abstractions.Options
open Dapr.Actors.Next.Testing
open Microsoft.Extensions.DependencyInjection
open Cart.Next.FSharp.Example01
open Xunit

type ActorRuntimeTests() =

    static member private CreateRuntime() =
        // Force-load the Glue assembly and invoke its generated registration module
        // (the module initializer does not fire for LoadFrom in this runtime version)
        let gluePath = System.IO.Path.Combine(AppContext.BaseDirectory, "Cart.Next.FSharp.Example01.Glue.dll")
        let glueAssembly = System.Reflection.Assembly.LoadFrom(gluePath)
        match glueAssembly.GetType("Dapr.Actors.Next.Generated.GeneratedActorRegistrationModule") with
        | null -> failwith "GeneratedActorRegistrationModule not found in Glue assembly"
        | moduleType ->
            match moduleType.GetMethod("Register", System.Reflection.BindingFlags.Static ||| System.Reflection.BindingFlags.NonPublic) with
            | null -> failwith "Register method not found"
            | registerMethod -> registerMethod.Invoke(null, null) |> ignore
        // Force the F# types to be loaded so the generated registry discovers them
        let _ = typeof<CartActor>
        ActorTestRuntime(fun services ->
            services.AddSingleton<IPricingClient, FakePricingClient>() |> ignore
            services.AddDaprActors(Action<DaprActorsOptions>(fun _ -> ())) |> ignore)

    [<Fact>]
    member this.FSharp_actor_types_work_with_ActorTestRuntime() = task {
        use runtime = ActorRuntimeTests.CreateRuntime()
        let cart = runtime.CreateActor<ICartActor>(ActorId.Create("cart-fsharp-int"), "Cart")

        let add = cart.AddItem({ Sku = "sku-1"; Quantity = 2 }, CancellationToken.None)
        do! runtime.RunToIdle()
        do! add

        let read = cart.GetSummary(CancellationToken.None)
        do! runtime.RunToIdle()
        let! summary = read

        Assert.Equal(2, summary.ItemCount)
        Assert.Equal(25.00m, summary.Total)
        Assert.False(summary.Abandoned)
    }

and FakePricingClient() =
    interface IPricingClient with
        member _.GetPriceAsync(sku: string, _: CancellationToken) : ValueTask<decimal> =
            ValueTask.FromResult(if sku = "sku-1" then 12.50m else 1.00m)
