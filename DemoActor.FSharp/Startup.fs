namespace DemoActor.FSharp

open Microsoft.AspNetCore.Builder
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.DependencyInjection
open Microsoft.Extensions.Hosting

open DemoActor.FSharp.BankService
open DemoActor.FSharp.BankActor

// Annorances:
//
// 1. Typical F# <-> ASP.NET impedence mismatches
type Startup() =
    member this.ConfigureServices(services: IServiceCollection) =
        services.AddSingleton<BankService>()
                .AddActors(fun options -> options.Actors.RegisterActor<BankActor> |> ignore)

    member this.Configure(app: IApplicationBuilder, env: IWebHostEnvironment) =
        if env.IsDevelopment() then
            app.UseDeveloperExceptionPage() |> ignore

        app.UseRouting()
           .UseEndpoints(fun endpoints ->
                endpoints.MapActorsHandlers() |> ignore
            ) |> ignore
