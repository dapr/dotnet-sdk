using Dapr.Actors.Runtime;

namespace Dapr.E2E.Test.Actors.Generators;

internal sealed record ActorWebApplicationOptions(Action<ActorRuntimeOptions> ConfigureActors)
{
    public ILoggerProvider? LoggerProvider { get; init; }

    public string? Url { get; init; }
}

internal sealed class ActorWebApplicationFactory
{
    public static WebApplication Create(ActorWebApplicationOptions options)
    {
        var builder = WebApplication.CreateBuilder();

        if (options.LoggerProvider is not null)
        {
            builder.Logging.ClearProviders();
            builder.Logging.AddProvider(options.LoggerProvider);
        }

        builder.Services.AddActors(options.ConfigureActors);

        var app = builder.Build();

        if (options.Url is not null)
        {
            app.Urls.Add(options.Url);
        }

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
