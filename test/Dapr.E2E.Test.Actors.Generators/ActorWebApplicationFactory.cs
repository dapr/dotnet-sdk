using Dapr.Actors.Runtime;

namespace Dapr.E2E.Test.Actors.Generators;

public sealed class ActorWebApplicationFactory
{
    public static WebApplication Create(Action<ActorRuntimeOptions> configure)
    {
        var builder = WebApplication.CreateBuilder();

        builder.Services.AddActors(configure);

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
