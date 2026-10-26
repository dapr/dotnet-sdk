using Dapr.Client;
using FrontendApp.Components;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// Services for server-side Blazor
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddHttpClient();

// Register the Dapr client
builder.Services.AddDaprClient();

var app = builder.Build();

app.UseStaticFiles();
app.UseAntiforgery();

#if NET9_0_OR_GREATER
app.MapStaticAssets();
#endif
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
