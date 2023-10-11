using GeneratedActor;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddActors(
    options =>
    {
        options.UseJsonSerialization = true;
        options.Actors.RegisterActor<RemoteActor>();
    });

var app = builder.Build();

app.UseRouting();

#pragma warning disable ASP0014
app.UseEndpoints(
    endpoints =>
    {
        endpoints.MapActorsHandlers();
    });

app.Run();
