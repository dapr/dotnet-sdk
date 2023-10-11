using Dapr.Actors.Runtime;

namespace Dapr.E2E.Test.Actors.Generators;

internal sealed record ActorWebApplicationOptions(Action<ActorRuntimeOptions> ConfigureActors)
{
    public Action<WebApplicationBuilder>? ConfigureBuilder { get; init; }
}

internal sealed class ActorWebApplicationFactory
{
    public static WebApplication Create(ActorWebApplicationOptions options)
    {
        var builder = WebApplication.CreateBuilder();

        options.ConfigureBuilder?.Invoke(builder);

        builder.Services.AddActors(options.ConfigureActors);

        var app = builder.Build();

        app.UseRouting();

        #pragma warning disable ASP0014
        app.UseEndpoints(
            endpoints =>
            {
                endpoints.MapActorsHandlers();
            });

        return app;
    }
}
